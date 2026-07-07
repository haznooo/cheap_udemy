using System;
using System.Collections.Generic;
using System.Text;

namespace Business.Dto.Rsponse
{
    public record UserProfileResponse(
     string? DisplayName,
     string? Bio,
      string? ImageUrl
        );
}
