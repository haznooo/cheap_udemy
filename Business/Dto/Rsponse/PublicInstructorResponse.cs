namespace Business.Dto.Rsponse
{
    // Public info about the person who published a course: username + the
    // profile's display name and avatar file name (both null if no profile row).
    public record PublicInstructorResponse(
        int UserId,
        string Username,
        string? DisplayName,
        string? ImageUrl
    );
}
