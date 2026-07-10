using AngleSharp.Io;
using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using Business.Interfaces;
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
    public class RefreshTokenService(AppDbContext context) : IRefreshTokenService
    {


        /// <summary>
        ///  used to creat a new token. with a brand new chain    
        /// </summary>
        public async Task<MyResult<RefreshTokenDto>> AddNewRefreshTokenFirstTime(int userId, string deviceInfo, string ipAddress, DateTime? expiresAt = null)
        {

            RefreshTokenRepository refreshTokenRepository = new RefreshTokenRepository(context);


            string refreshToken = GenerateRefreshToken();
            string RefreshTokenHashed = HashRefreshToken(refreshToken);

            var NewToken = await refreshTokenRepository.AddRefreshTokenAsync(new RefreshTokenEntity
            {
                user_id = userId,
                token_hash = RefreshTokenHashed,
                created_at = DateTime.UtcNow,
                expires_at = expiresAt ?? DateTime.UtcNow.AddDays(7),
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


        // Exchanges a refresh token for a new access token + a rotated refresh token. Enforces
        // rotation reuse detection (a superseded token replayed = theft → kill the whole chain) and
        // absolute expiration (the new token inherits the parent's expiry, so a chain dies at most
        // 7 days after the ORIGINAL login). Returns a LoginResponse the controller mints a JWT from.
        public async Task<MyResult<LoginResponse>> RefreshAccessToken(string refreshToken, int userId, string deviceInfo, string ipAddress)
        {
            if (userId <= 0)
                return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "invalid user id");

            if (string.IsNullOrWhiteSpace(refreshToken))
                return MyResult<LoginResponse>.Failure(ErrorType.BadRequest, "refresh token is required");

            // Validate the user exists and is active. If the user was deleted or deactivated, we don't want to issue a new token
            // or even doing a lookup on the refresh token. This prevents a deleted user from being able to use a refresh token to get a new access token.

            UserAndProfileRepository userRepository = new UserAndProfileRepository(context);
            var user = await userRepository.GetUserByIdAsync(userId);

            if (user == null)
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "user no longer exists");

            RefreshTokenRepository refreshTokenRepository = new RefreshTokenRepository(context);

            // Look the token up across ALL states (SHA-256 is deterministic → direct indexed lookup).
            // We need used/revoked/expired rows too, so a replayed used token is visible below.
            string tokenHash = HashRefreshToken(refreshToken);
            var currentToken = await refreshTokenRepository.GetRefreshTokenByHashAsync(userId, tokenHash);

            // Never existed / wrong token → plain 401, no breach.
            if (currentToken == null)
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid refresh token");

            // REUSE DETECTION: this token was already rotated away (it points at a child via
            // replaced_by_id) yet is being presented again → two parties hold tokens on this chain
            // (the real user AND a thief). Kill the entire chain so both must re-login. We rely on
            // replaced_by_id (not is_used) so a logged-out token — used, but with no child — does
            // NOT false-positive as a breach; it falls through to the plain 401 below.
            if (currentToken.replaced_by_id != null)
            {
                var revokedChain = await refreshTokenRepository.RevokeBreachedChainAsync(currentToken.token_id);
                if (!revokedChain)
                    return MyResult<LoginResponse>.Failure(ErrorType.Failure, "failed to revoke breached token chain");

                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "refresh token reuse detected");
            }

            // Dead but not a breach: used (e.g. logged out), revoked, or simply expired → re-login.
            if (currentToken.is_used || currentToken.revoked_at != null || currentToken.expires_at <= DateTime.UtcNow)
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid or expired refresh token");

            // Issue the replacement token first so we can link the old one to it. The child INHERITS
            // the parent's expiry (absolute expiration) — no sliding 7-day reset on every refresh.
            var newTokenResult = await AddNewRefreshTokenFirstTime(userId, deviceInfo, ipAddress, currentToken.expires_at);

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

            string tokenHash = HashRefreshToken(refreshToken);
            var currentToken = await refreshTokenRepository.GetValidRefreshTokenByHashAsync(userId, tokenHash);

            if (currentToken != null)
            {
                currentToken.revoked_at = DateTime.UtcNow;
                currentToken.is_used = true;
                currentToken.last_used_at = DateTime.UtcNow;
                await refreshTokenRepository.UpdateRefreshTokenAsync(currentToken);
            }

            return MyResult<bool>.Success(true);
        }


        // Revokes ALL of a user's active refresh tokens (every device/chain). Called when the
        // credential changes (password update) so any session opened with the old password —
        // including one held by a thief — is forced to re-login.
        public async Task<MyResult<bool>> RevokeAllForUser(int userId)
        {
            if (userId <= 0)
                return MyResult<bool>.Failure(ErrorType.BadRequest, "invalid user id");

            RefreshTokenRepository refreshTokenRepository = new RefreshTokenRepository(context);
            var revoked = await refreshTokenRepository.RevokeAllRefreshTokensAsync(userId);

            if (revoked < 0)
                return MyResult<bool>.Failure(ErrorType.Failure, "failed to revoke refresh tokens");

            return MyResult<bool>.Success(true);
        }


        public string GenerateRefreshToken()
        {


            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);

        }

        //there is no need for bCrypt here . we do not need salting for a value that is already random
        public static string HashRefreshToken(string refreshToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
            return Convert.ToHexString(bytes);
        }
    }
}
