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



        // Mints and stores a new refresh token row. Used both for a brand-new chain (login/signup,
        // expiresAt == null → fresh 7-day deadline) AND for rotation (expiresAt == the parent's
        // expiry, so the new token INHERITS the original login deadline — see absolute expiration).
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
            // (the real user AND a thief). Kill the entire chain so both must re-login. We key on
            // replaced_by_id (not is_used) so a logged-out token — used, but with no child — does
            // NOT false-positive as a breach; it falls through to the plain 401 below.
            if (currentToken.replaced_by_id != null)
            {
                await RevokeBreachedChainAsync(refreshTokenRepository, currentToken);
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "refresh token reuse detected");
            }

            // Dead but not a breach: used (e.g. logged out), revoked, or simply expired → re-login.
            if (currentToken.is_used || currentToken.revoked_at != null || currentToken.expires_at <= DateTime.UtcNow)
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "invalid or expired refresh token");

            // --- token is valid; rotate it ---

            // Bug 1 fix: validate the user BEFORE touching any tokens. A deleted/banned user
            // must fail cleanly with 401 — not after we've already burned the old token and
            // minted an orphan replacement.
            UserAndProfileRepository userRepository = new UserAndProfileRepository(context);
            var user = await userRepository.GetUserByIdAsync(userId);

            if (user == null)
                return MyResult<LoginResponse>.Failure(ErrorType.Unauthorized, "user no longer exists");

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

        // Walks replaced_by_id FORWARD from a superseded token and kills every token on that chain,
        // including the thief's currently-valid one. Scoped to the chain only — NEVER by user_id —
        // so the user's other devices (separate chains) stay logged in. Sets chain_breached as an
        // audit marker. Already-revoked links keep their original revoked_at.
        private static async Task RevokeBreachedChainAsync(RefreshTokenRepository repo, RefreshTokenEntity start)
        {
            var node = start;
            while (node != null)
            {
                node.chain_breached = true;
                node.is_used = true;
                node.revoked_at ??= DateTime.UtcNow;
                await repo.UpdateRefreshTokenAsync(node);

                if (node.replaced_by_id == null)
                    break;

                node = await repo.GetRefreshTokenByIdAsync(node.replaced_by_id.Value);
            }
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


        public string GenerateRefreshToken()
        {


            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);

        }

        // Refresh tokens are high-entropy random values (64 bytes from a CSPRNG), so unlike passwords
        // they don't need a salted/slow hash. A fast, deterministic SHA-256 is the right fit: it's
        // irreversible (safe at rest) AND deterministic, so the stored hash can be matched with a
        // direct indexed equality lookup. (Passwords still use BCrypt — different threat model.)
        public static string HashRefreshToken(string refreshToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
            return Convert.ToHexString(bytes);
        }
    }
}
