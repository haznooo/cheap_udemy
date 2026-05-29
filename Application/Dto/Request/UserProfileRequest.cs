namespace Business.Dto.Request
{
    public record UserProfileRequest(
     string? DisplayName,
     string? Bio,
     string? ImageUrl,
     int? CountryId
 );
}
