namespace DataAccess.Dto
{
    // Slim row for the admin "list all users" read (GET api/admin/users).
    // Deliberately NOT the UserEntity (which carries the password hash) and NOT the
    // full profile — just the columns an admin scans in a user table. Full account +
    // profile detail stays on the single-user read (GET api/admin/users/{userId}).
    public class UserListItemDto
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public DateTime CreateDate { get; set; }
    }
}
