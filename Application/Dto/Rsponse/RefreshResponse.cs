namespace Business.Dto.Rsponse
{
    // Response for api/User/refresh — tokens ONLY, no user/profile info.
    // Refresh is a token-exchange operation; the client already knows who it is,
    // so echoing identity/profile back would be needless data exposure.
    public class RefreshResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }
    }
}
