using Microsoft.AspNetCore.Authorization;

namespace Api.Authorization
{
    public class UserOwnerOrAdminRequirement : IAuthorizationRequirement
    {
        // This class represents the authorization rule itself.
        // It does NOT contain logic.
        // It simply defines the requirement:
        // "Owner OR Admin can access the student resource."
       
    }
}
