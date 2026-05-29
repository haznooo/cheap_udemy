using System;
using System.Collections.Generic;
using System.Text;

namespace Business.Dto.Rsponse
{
    public class RefreshTokenResponse
    {
        public string DeviceInfo { get; set; }
        public string RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string IpAddress { get; set; }


    }
}
