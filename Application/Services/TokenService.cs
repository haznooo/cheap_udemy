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



        public async Task<MyResult<RefreshTokenDto>> AddNewRefreshTokenFirstTime(RefreshTokenRequest request, string deviceInfo, string ipAddress)
        {

            RefreshTokenRepository refreshTokenRepository = new RefreshTokenRepository(context);


            string refreshToken = GenerateRefreshToken();
            string RefreshTokenHashed = BCrypt.Net.BCrypt.HashPassword(refreshToken);

            var NewToken = await refreshTokenRepository.AddRefreshTokenAsync(new RefreshTokenEntity
            {
                user_id = request.UserId,
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
        public async Task<MyResult<LoginResponse>> RefreshAccessToken(RefreshTokenRequest request, string deviceInfo, string ipAddress)
        {
            if (request.UserId <= 0)
                return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "invalid user id");

            if (string.IsNullOrWhiteSpace(request.RefreshToken))
                return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "refresh token is required");

            RefreshTokenRepository refreshTokenRepository = new RefreshTokenRepository(context);

            // Find the matching, still-usable token by verifying against every valid hash for the user.
            var validTokens = await refreshTokenRepository.GetValidRefreshTokensByUserIdAsync(request.UserId);
            var currentToken = validTokens
                .FirstOrDefault(t => BCrypt.Net.BCrypt.Verify(request.RefreshToken, t.token_hash));

            if (currentToken == null)
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid or expired refresh token");

            // Issue the replacement token first so we can link the old one to it.
            var newTokenResult = await AddNewRefreshTokenFirstTime(new RefreshTokenRequest
            (
                RefreshToken: null,
                UserId: request.UserId
            ), deviceInfo, ipAddress);

            if (!newTokenResult.IsSuccess)
                return MyResult<LoginResponse>.Failure(ErrorType.Failure, "failed to issue refresh token");

            // Rotation: retire the old token and chain it to the new one.
            currentToken.is_used = true;
            currentToken.revoked_at = DateTime.UtcNow;
            currentToken.last_used_at = DateTime.UtcNow;
            currentToken.replaced_by_id = newTokenResult.Value.RefreshTokenId;
            await refreshTokenRepository.UpdateRefreshTokenAsync(currentToken);

            // Fetch the user so the controller can mint a JWT with the right claims.
            UserAndProfileRepository userRepository = new UserAndProfileRepository(context);
            var user = await userRepository.GetUserByIdAsync(request.UserId);

            if (user == null)
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "user no longer exists");

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
        public async Task<MyResult<bool>> RevokeRefreshToken(RefreshTokenRequest request)
        {
            if (request.UserId <= 0 || string.IsNullOrWhiteSpace(request.RefreshToken))
                return MyResult<bool>.Success(true);

            RefreshTokenRepository refreshTokenRepository = new RefreshTokenRepository(context);

            var validTokens = await refreshTokenRepository.GetValidRefreshTokensByUserIdAsync(request.UserId);
            var currentToken = validTokens
                .FirstOrDefault(t => BCrypt.Net.BCrypt.Verify(request.RefreshToken, t.token_hash));

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
