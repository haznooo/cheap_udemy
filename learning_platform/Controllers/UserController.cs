using Business.Common;
using Business.Dto.Request;
using Business.Dto.Rsponse;
using Business.Services;
using DataAccess.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController(AppDbContext context) : ControllerBase
    {


        [HttpPost("Delete/{userId}")] // Use route parameters for IDs
        public async Task<ActionResult<bool>> DeleteUser(int userId, [FromBody] DeleteUserRequest request, [FromServices] IAuthorizationService authorizationService)
        {
          
            var authResult = await authorizationService.AuthorizeAsync(User, userId, "UserOwnerOrAdmin");
            if (!authResult.Succeeded) return Forbid();


            UserService userService = new UserService(context);

            var result = await userService.DeleteUser(userId, request);

            if (!result.IsSuccess)
            {
                // Handle all failure types in one switch expression
                return result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.Unauthorized => Conflict(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(result.Value);
        }

        [HttpPost("UpdatePassword/{userId}")] // Use route parameters for IDs
        public async Task<ActionResult<bool>> UpdatePassword(int userId, [FromBody] UpdatePasswordRequest request, [FromServices] IAuthorizationService authorizationService)
        {

            var authResult = await authorizationService.AuthorizeAsync(User, userId, "UserOwnerOrAdmin");
            if (!authResult.Succeeded) return Forbid();


            UserService userService = new UserService(context);

            var result = await userService.UpdatePassword(userId, request);

            if (!result.IsSuccess)
            {
                // Handle all failure types in one switch expression
                return result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.Unauthorized => Conflict(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }

            return Ok(result.Value);
        }

        //works fine
        [HttpPut("UpdateProfile/{userId}")]
        public async Task<ActionResult<UserProfileResponse>> UpdateUserProfile(int userId, [FromBody] UserProfileRequest ProfileRequest, [FromServices] IAuthorizationService authorizationService)
        {

            var authResult = await authorizationService.AuthorizeAsync(User, userId, "UserOwnerOrAdmin");
            if (!authResult.Succeeded) return Forbid();

            UserService userService = new UserService(context);
            var result = await userService.UpdateUserProfile(userId, ProfileRequest);

            if (!result.IsSuccess)
            {
                // Handle all failure types in one switch expression
                return result.FailureType switch
                {
                    ErrorType.NotFound => NotFound(result.Errors),
                    ErrorType.BadRequest => BadRequest(result.Errors),
                    ErrorType.Conflict => Conflict(result.Errors),
                    ErrorType.Unauthorized => Conflict(result.Errors),
                    _ => StatusCode(500, "An unexpected error occurred")
                };
            }
            return result.Value;
        }
    }

}
