using AngleSharp.Io;
using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Repositories;
using MediatR;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Business.Services
{
    public class TokenService(AppDbContext context)
    {



        public async Task<MyResult<RefreshTokenDto>> AddNewRefreshTokenFirstTime(int userId, string deviceInfo, string ipAddress)
        {

            RefreshTokenRepository refreshTokenRepository = new RefreshTokenRepository(context);


            string refreshToken = GenerateRefreshToken();
            string RefreshTokenHashed = BCrypt.Net.BCrypt.HashPassword(refreshToken);

            var NewToken = await refreshTokenRepository.AddRefreshTokenAsync(new RefreshTokenEntity
            {
                user_id = userId,
                token_hash = RefreshTokenHashed,
                expires_at = DateTime.UtcNow.AddDays(7),
                is_used = false,
                device_info = deviceInfo,
                ip_address = ipAddress,

            }
            );

            if(NewToken == null)
            {
                return MyResult<RefreshTokenDto>.Failure(ErrorType.Failure, "failed to create refresh token");
            }

            return MyResult<RefreshTokenDto>.Success(new RefreshTokenDto
            {
                RefreshTokenId = NewToken?.token_id,
                RefreshToken = refreshToken,
                RefreshTokenHash = RefreshTokenHashed,
                DeviceInfo = NewToken?.device_info,
                ExpiresAt = NewToken?.expires_at,
                IpAddress = NewToken?.ip_address,
             
            });

        }


        // Validates the presented refresh token, rotates it (old one is marked used + revoked and
        // points to its replacement), and returns a LoginResponse the controller fills with a new
        // access token (JWT). Same simple "one row per login" model used by AddNewRefreshTokenFirstTime.
        public async Task<MyResult<LoginResponse>> RefreshAccessToken(string refreshToken, int userId, string deviceInfo, string ipAddress)
        {
            if (userId <= 0)
                return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "invalid user id");

            if (string.IsNullOrWhiteSpace(refreshToken))
                return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "refresh token is required");

            RefreshTokenRepository refreshTokenRepository = new RefreshTokenRepository(context);

            // Find the matching, still-usable token by verifying against every valid hash for the user.
            var validTokens = await refreshTokenRepository.GetValidRefreshTokensByUserIdAsync(userId);
            var currentToken = validTokens
                .FirstOrDefault(t => BCrypt.Net.BCrypt.Verify(refreshToken, t.token_hash));

            if (currentToken == null)
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid or expired refresh token");

            // Bug 1 fix: validate the user BEFORE touching any tokens. A deleted/banned user
            // must fail cleanly with 401 — not after we've already burned the old token and
            // minted an orphan replacement.
            UserAndProfileRepository userRepository = new UserAndProfileRepository(context);
            var user = await userRepository.GetUserByIdAsync(userId);

            if (user == null)
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "user no longer exists");

            // Issue the replacement token first so we can link the old one to it.
            var newTokenResult = await AddNewRefreshTokenFirstTime(userId, deviceInfo, ipAddress);

            if (!newTokenResult.IsSuccess)
                return MyResult<LoginResponse>.Failure(ErrorType.Failure, "failed to issue refresh token");

            // Rotation: retire the old token and chain it to the new one.
            currentToken.is_used = true;
            currentToken.revoked_at = DateTime.UtcNow;
            currentToken.last_used_at = DateTime.UtcNow;
            currentToken.replaced_by_id = newTokenResult.Value.RefreshTokenId;

            // Bug 2 fix: don't ignore the revoke result. If it silently fails we'd be left with
            // two valid tokens (the new one plus the still-active old one), so fail loudly.
            var revoked = await refreshTokenRepository.UpdateRefreshTokenAsync(currentToken);
            if (revoked == null)
                return MyResult<LoginResponse>.Failure(ErrorType.Failure, "failed to rotate refresh token");

            return MyResult<LoginResponse>.Success(new LoginResponse
            {
                Id = user.UserId,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                IsRefreshTokenRevoked = false,
                RefreshToken = newTokenResult.Value.RefreshToken,
                RefreshTokenExpiresAt = newTokenResult.Value.ExpiresAt
            });
        }

        // Revokes the presented refresh token (logout). Never reveals whether the token existed.
        public async Task<MyResult<bool>> RevokeRefreshToken(string refreshToken, int userId)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(refreshToken))
                return MyResult<bool>.Success(true);

            RefreshTokenRepository refreshTokenRepository = new RefreshTokenRepository(context);

            var validTokens = await refreshTokenRepository.GetValidRefreshTokensByUserIdAsync(userId);
            var currentToken = validTokens
                .FirstOrDefault(t => BCrypt.Net.BCrypt.Verify(refreshToken, t.token_hash));

            if (currentToken != null)
            {
                currentToken.revoked_at = DateTime.UtcNow;
                currentToken.is_used = true;
                currentToken.last_used_at = DateTime.UtcNow;
                await refreshTokenRepository.UpdateRefreshTokenAsync(currentToken);
            }

            return MyResult<bool>.Success(true);
        }


        public string GenerateRefreshToken()
        {


            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);

        }
    }
}
