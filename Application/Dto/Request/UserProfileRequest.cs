namespace Business.Dto.Request
{
    // No ImageUrl here on purpose: the avatar is never a client-supplied file name.
    // It is set only through POST api/user/me/avatar, where the server uploads the
    // file itself and generates the stored name.
    public record UserProfileRequest(
     string? DisplayName,
     string? Bio
 );
}
