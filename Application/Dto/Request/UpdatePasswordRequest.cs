using System;
using System.Collections.Generic;
using System.Text;

namespace Business.Dto.Request
{
    public class UpdatePasswordRequest
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; } = string.Empty;
    }
}
