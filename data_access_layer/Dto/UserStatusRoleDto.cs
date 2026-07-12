namespace DataAccess.Dto
{
    // Internal-only lookup type for the admin ban/suspend/unban flow: status + role
    // in a single query, deliberately NOT filtered by status (the target of a ban
    // check is usually not "active"). Never serialized to the client.
    public class UserStatusRoleDto
    {
        public string Status { get; set; }
        public string Role { get; set; }
    }
}
