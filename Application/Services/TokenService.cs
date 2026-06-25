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



        public async Task<MyResult<RefreshTokenDto>> AddNewRefreshTokenFirstTime(RefreshTokenRequest request)
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
                device_info = request.deviceInfo,
                ip_address = request.IpAddress,

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


        public string GenerateRefreshToken()
        {


            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);

        }
    }
}
