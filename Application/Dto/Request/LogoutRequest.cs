namespace Business.Dto.Request
{
    public record LogoutRequest
    (
       string Email,
        string RefreshToken
    );
}
