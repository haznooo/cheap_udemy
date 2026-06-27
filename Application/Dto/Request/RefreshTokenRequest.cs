namespace Business.Dto.Request
{
    // Sent to api/User/refresh and api/User/logout.
    // The user id is NOT taken from the client anymore — it is read from the (possibly expired)
    // access token's signed claims. The client therefore sends BOTH tokens:
    //   - RefreshToken: the token being exchanged / revoked.
    //   - AccessToken:  the last access token (may be expired); used ONLY to recover the user id.
    public record RefreshTokenRequest
    (
        string RefreshToken,
        string AccessToken
    );
}
