using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;

namespace Business.Interfaces
{
    public interface IAuthenticationService
    {
        Task<MyResult<LoginResponse>> UserSignUp(SignUpRequest request, string deviceInfo, string ipAddress);
        Task<MyResult<LoginResponse>> LoginUser(LoginRequest request, string deviceInfo, string ipAddress);
    }
}
