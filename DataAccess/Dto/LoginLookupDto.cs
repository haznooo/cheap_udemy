namespace DataAccess.Dto
{
    // Internal-only lookup type for the login flow: carries the password hash so the
    // whole login can run off a single query. NEVER serialized to the client (that's
    // what UserAndProfileDto / LoginResponse are for) — keep the hash out of those.
    public class LoginLookupDto
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public string? HashedPassword { get; set; }   // NULL for anonymized/deleted users
        public UserProfileDto? Profile { get; set; }
    }
}
