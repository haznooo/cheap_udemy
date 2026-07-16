namespace Business.Dto.Request
{
    // Sent to api/User/refresh and api/User/logout.
    // Only the refresh token travels in the body. The user id is NOT taken from the client —
    // it is read from the (possibly expired) access token supplied in the standard
    // "Authorization: Bearer <token>" header, exactly like every other endpoint. The header
    // token may be expired; it is used ONLY to recover the user id (signature still verified).
    public record RefreshTokenRequest
    (
        string RefreshToken
    );
}
