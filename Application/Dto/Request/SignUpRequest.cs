namespace Business.Dto.Request
{
    public record SignUpRequest
    (
      string Username,
    string Email,
    string Password,
    UserProfileRequest? Profile = null // The entire profile object is optional
    );
}
