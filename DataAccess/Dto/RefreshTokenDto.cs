using System;
using System.Collections.Generic;
using System.Text;

namespace DataAccess.Dto
{
    public class RefreshTokenDto
    {
        public int? RefreshTokenId { get; set; }
        public string? RefreshToken { get; set; }
        public string? RefreshTokenHash { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }

        public string? DeviceInfo { get; set; }

        public string? IpAddress { get; set; }
    }
}
