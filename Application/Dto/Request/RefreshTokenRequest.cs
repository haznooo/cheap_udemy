namespace Business.Dto.Request
{
    public record RefreshTokenRequest
    (

       string RefreshToken,
       string deviceInfo,
       string IpAddress,
        int UserId
        );
}
