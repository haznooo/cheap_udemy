
using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using DataAccess.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Data;
using static DataAccess.Common.clsPageResult;


namespace DataAccess.Repositories
{
    public class UserAndProfileRepository(AppDbContext context) : IUserAndProfileRepository
    {
    

        public async Task<UserAndProfileDto> AddUserAsync(UserEntity User)
        {
            try
            {
                var newUser = new UserEntity
                {
                    user_id = 0, // Let the database generate the ID
                    username = User.username,
                    create_date = DateTime.UtcNow,
                    status = User.status,
                    email = User.email,
                    hashed_password = User.hashed_password,
                    role = User.role
                };


                await context.Users.AddAsync(newUser);
                await context.SaveChangesAsync();

                return new UserAndProfileDto
                {
                    UserId = newUser.user_id,
                    Username = newUser.username,
                    Email = newUser.email,
                    Role = newUser.role,
                    Status = newUser.status,
                    Profile = null
                };

            }
            catch (Exception ex)
            {
                //latter will be moved it to a log 
                Console.WriteLine(ex);
                return null;
            }

        }
        public async Task<bool> DeleteUserAsync_Anonymize(int userId)
        {
            try
            {
                // 1. Get a direct connection to the underlying database command system
                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = "DELETE FROM users WHERE user_id = @userId";
                command.CommandType = CommandType.Text;
                // Add the parameter safely to prevent SQL injection
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@userId";
                parameter.Value = userId;
                command.Parameters.Add(parameter);
                // 2. Open connection if closed and execute BLINDLY
                if (context.Database.GetDbConnection().State != ConnectionState.Open)
                {
                    await context.Database.OpenConnectionAsync();
                }
                // ExecuteNonQuery executes the SQL and doesn't check or throw errors based on row counts!
                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString()); return false;
            }
            finally
            {
                // Clean up connection tracking if necessary
                await context.Database.CloseConnectionAsync();
            }
        }
        public async Task<bool> UpdateUserPasswordAsync(int userId, string newHashedPassword)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.user_id == userId);
            if (user == null) return false;

            user.hashed_password = newHashedPassword;
            await context.SaveChangesAsync();
            return true;
        }
        public async Task<LoginLookupDto?> GetUserForLoginAsync(string email)
        {
            var R = await context.Users.Where(u => u.email == email)
             .Select(u => new
             {
                 UserId = u.user_id,
                 u.username,
                 u.email,
                 u.role,
                 u.status,
                 u.hashed_password,

                 HasProfile = u.UserProfile != null,
                 u.UserProfile.bio,
                 u.UserProfile.image_url,
                 u.UserProfile.display_name
             }).FirstOrDefaultAsync();

            if (R == null) return null;

            return new LoginLookupDto
            {
                UserId = R.UserId,
                Username = R.username,
                Email = R.email,
                Role = R.role,
                Status = R.status,
                HashedPassword = R.hashed_password,
                Profile = R.HasProfile
                    ? new UserProfileDto
                    {
                        DisplayName = R.display_name,
                        Bio = R.bio,
                        ImageUrl = R.image_url
                    }
                    : null
            };
        }
        public async Task<UserAndProfileDto> GetUserByIdAsync(int userId)
        {

            var R = await context.Users.Where(u => u.user_id == userId & u.status == "active")
            .Select(u => new
            {
                // Fields from the User table
                UserId = u.user_id,
                u.username,
                u.email,
                u.role,
                u.status,


                // Fields from the UserProfile navigation property
                u.UserProfile.bio,
                u.UserProfile.image_url,
                u.UserProfile.display_name
            }).FirstOrDefaultAsync();

            // No active user with this id (deleted/banned/missing). Return null instead of
            // dereferencing R below, which would throw a NullReferenceException.
            if (R == null)
                return null;

            UserProfileDto response = new UserProfileDto
            {

                DisplayName = R.display_name,
                Bio = R.bio,
                ImageUrl = R.image_url

            };




            return new UserAndProfileDto
            {

                UserId = R.UserId,
                Username = R.username,
                Email = R.email,
                Role = R.role,
                Status = R.status,
                Profile = response

            };


        }


        // Public-facing read: username + profile display name/avatar for any ACTIVE
        // user (used to show the instructor who published a course). Active-only so a
        // deleted/anonymized or banned account doesn't surface; profile fields are
        // null when the user has no profile row. LEFT join via the nav property.
        public async Task<PublicUserDto?> GetPublicUserInfoAsync(int userId)
        {
            return await context.Users
                .Where(u => u.user_id == userId && u.status == "active")
                .AsNoTracking()
                .Select(u => new PublicUserDto
                {
                    UserId = u.user_id,
                    Username = u.username,
                    DisplayName = u.UserProfile.display_name,
                    ImageUrl = u.UserProfile.image_url
                })
                .FirstOrDefaultAsync();
        }

        // profile
        public async Task<UserProfileDto?> GetUserProfileByIdAsync(int userId)
        {
            return await context.UsersProfile
                .Where(p => p.user_id == userId)
                .AsNoTracking()
                .Select(p => new UserProfileDto
                {
                    DisplayName = p.display_name,
                    Bio = p.bio,
                    ImageUrl = p.image_url
                })
                .FirstOrDefaultAsync();
        }

        // Returns the replaced avatar file name (null if the user had none) so the
        // caller can remove the stale file from storage after the change is saved.
        public async Task<string?> UpdateUserAvatarAsync(int userId, string fileName)
        {
            var profile = await context.UsersProfile.FirstOrDefaultAsync(p => p.user_id == userId);

            string? oldFileName = null;

            // Signup no longer creates a profile row, so a new user may set an avatar
            // before ever creating a profile — create the row on the fly instead of
            // failing (the uploaded file would otherwise be orphaned in the bucket).
            if (profile == null)
            {
                context.UsersProfile.Add(new UserProfileEntity { user_id = userId, image_url = fileName });
            }
            else
            {
                oldFileName = profile.image_url;
                profile.image_url = fileName;
            }

            await context.SaveChangesAsync();
            return oldFileName;
        }

        // Clears the user's avatar. Returns the REMOVED file name (null if there
        // was no profile row or no avatar set) so the caller can delete the stale
        // file from storage. No row is created here — nothing to remove.
        public async Task<string?> RemoveUserAvatarAsync(int userId)
        {
            var profile = await context.UsersProfile.FirstOrDefaultAsync(p => p.user_id == userId);

            if (profile == null || string.IsNullOrEmpty(profile.image_url)) return null;

            var oldFileName = profile.image_url;
            profile.image_url = null;

            await context.SaveChangesAsync();
            return oldFileName;
        }

        public async Task<UserProfileEntity> AddUserProfileAsync(int UserId, UserProfileEntity NewUserProfileData)
        {
            var userProfileE = new UserProfileEntity
            {
                user_id = UserId,
                bio = NewUserProfileData.bio,
                display_name = NewUserProfileData.display_name
            };

            context.UsersProfile.Add(userProfileE);
            await context.SaveChangesAsync();

            return userProfileE;
        }

        public async Task<UserProfileEntity> UpdateUserProfileByUserIdAsync(int UserId, UserProfileEntity NewUserProfileData)
        {
            var userProfileE = await context.UsersProfile
                .FirstOrDefaultAsync(up => up.user_id == UserId);

            if (userProfileE == null) return null;

            // image_url is deliberately NOT updated here — the avatar is managed only by
            // UpdateUserAvatarAsync (via the avatar upload endpoint). Copying it from the
            // incoming data would wipe the existing avatar on every profile update.
            userProfileE.bio = NewUserProfileData.bio;
            userProfileE.display_name = NewUserProfileData.display_name;

            await context.SaveChangesAsync();

            return userProfileE;
        }

        //user utitlity
        public async Task<bool> IsEmailUsedAsync(string email)
        {

            return await context.Users.AnyAsync(e => e.email == email);

        }
        public async Task<bool> IsUsernameUsedAsync(string username)
        {

            return await context.Users.AnyAsync(e => e.username == username);

        }
        public async Task<bool> IsUserActiveAsync(int userId)
        {
            return await context.Users.AnyAsync(e => e.user_id == userId && e.status == "active");
        }
        public async Task<bool> DoesUserProfileExistAsync(int userId)
        {
            return await context.UsersProfile.AnyAsync(p => p.user_id == userId);
        }
        public async Task<bool> PromoteUserToInstructorAsync(int userId)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.user_id == userId);
            if (user == null) return false;
            if (user.role == "student")
            {
                user.role = "instructor";
                await context.SaveChangesAsync();
            }
            return true;
        }
        public async Task<string?> GetUserRoleAsync(int userId)
        {
            return await context.Users
                .Where(u => u.user_id == userId && u.status == "active")
                .Select(u => u.role)
                .FirstOrDefaultAsync();
        }
        public async Task<string?> GetHashedPasswordByIdAsync(int userId)
        {
            string? hashedPassword = await context.Users.Where(u => u.user_id == userId).Select(u => u.hashed_password).FirstOrDefaultAsync();

            return hashedPassword;
        }
    }
}
