
using DataAccess.Data;
using DataAccess.Dto;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;
using static DataAccess.Common.clsPageResult;


namespace DataAccess.Repositories
{
    public class UserAndProfileRepository(AppDbContext context)
    {
    
        //user
        public async Task<UserAndProfileDto> AddUserAsync(UserEntity User)
        {
            try
            {
                UserProfileEntity userProfileEntity = null;
                if (User.UserProfile != null)
                {
                    userProfileEntity = new UserProfileEntity
                    {
                        user_id = User.user_id,
                        bio = User.UserProfile?.bio,
                        image_url = User.UserProfile?.image_url,
                        country_id = User.UserProfile?.country_id,
                        display_name = User.UserProfile?.display_name,

                    };

                }
                var newUser = new UserEntity
                {
                    user_id = 0, // Let the database generate the ID
                    username = User.username,
                    create_date = DateTime.UtcNow,
                    status = User.status,
                    email = User.email,
                    hashed_password = User.hashed_password,
                    role = User.role,
                    UserProfile = userProfileEntity // This will be safely null if no profile was provided
                };

      
                await context.Users.AddAsync(newUser);
                await context.SaveChangesAsync();

                var countryInnerInfo = new { CountryName = (string?)null, CountryIsoCode = (string?)null };

                if (newUser.UserProfile?.country_id != null) 
                {

                     countryInnerInfo = await context.UsersProfile
                .Where(u => u.user_id == newUser.user_id)
                   .Select(u => new
                   {
                       CountryName = u.country.name,
                       CountryIsoCode = u.country.iso_code
                   })
                   .FirstOrDefaultAsync();

                }
      
                return new UserAndProfileDto 
                {
                    UserId = newUser.user_id,
                    Username = newUser.username,
                    Email = newUser.email,
                    Role = newUser.role,
                    Status = newUser.status,
                    Profile = new UserProfileDto
                    {
                        Bio = newUser.UserProfile?.bio,
                        ImageUrl = newUser.UserProfile?.image_url,
                        DisplayName = newUser.UserProfile?.display_name,
                        CountryId = newUser.UserProfile?.country_id,
                        CountryName = countryInnerInfo?.CountryName,
                        CountryIsoCode = countryInnerInfo?.CountryIsoCode
                    }
                };

            }
            catch (Exception ex)
            {
                //latter will be moved it to a log 
                Console.WriteLine(ex);
                return null;
            }

        }
        public async Task<bool> DeleteUserAsync_Anonymize(UserEntity User)
        {
      //the reason i did this is because i prevented the normal delete method to use soft delete (so i can keep the id safe for referencing other stuff in the project 
      // inside the delete trigger i have an update prcoess so i can take the old id of the user and use it for a generic gmail : delete_67@app.com
      // i really don't remember it in detials but since there is no actual delete the trigger was only retuning null and it did not work well with EF
      // i tried to make it return some sort of value like a bool but it seems like this is not allowed in postgre 
            try
            {
                // 1. Get a direct connection to the underlying database command system
                using var command = context.Database.GetDbConnection().CreateCommand();

                command.CommandText = "DELETE FROM users WHERE user_id = @userId";
                command.CommandType = CommandType.Text;

                // Add the parameter safely to prevent SQL injection
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@userId";
                parameter.Value = User.user_id;
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
        public async Task<bool> DeleteUserAsync_Anonymize(int userId)
        {

            // read "DeleteUserAsync_Anonymize(UserEntity User)" to understand this 
            UserEntity user = await context.Users.FirstOrDefaultAsync(u => u.user_id == userId);
            if(user == null) return false;
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

        public async Task<PageResult<UserEntity>> GetUsersAsync(int pageNumber, int pageSize)
        {
            var query = context.Users.AsNoTracking(); // Better performance for Read-Only

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PageResult<UserEntity>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }
        public async Task<UserAndProfileDto> GetUserByCredentialsAsync(string email, string hashed_password)
        {

            var User = await context.Users.Where(u => u.email == email && u.status == "active")
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
               u.UserProfile.display_name,
               u.UserProfile.country.iso_code,
              u.UserProfile.country_id,
               u.UserProfile.country.name
           }).FirstOrDefaultAsync();

            if(User == null) return null;   

            return new UserAndProfileDto
            {
                UserId = User.UserId,
                Username = User.username,
                Email = User.email,
                Role = User.role,
                Status = User.status,
                Profile = new UserProfileDto
                {
                   Bio = User.bio,
                 ImageUrl = User.image_url,
                    DisplayName = User.display_name,
                   CountryId = User.country_id,
                   CountryName = User.name,
                    CountryIsoCode = User.iso_code
                }
            };




        }
        public async Task<UserAndProfileDto> GetUserByEmailAsync(string email)
        {
            var R = await context.Users.Where(u => u.email == email && u.status == "active")
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
                 u.UserProfile.display_name,
                 u.UserProfile.country.iso_code,
                 u.UserProfile.country_id,
                 u.UserProfile.country.name
             }).FirstOrDefaultAsync();

            if (R == null) return null;

            UserProfileDto response = new UserProfileDto
            {

                DisplayName = R.display_name,
                Bio = R.bio,
                ImageUrl = R.image_url,
                CountryId = R.country_id,
                CountryName = R.name,
                CountryIsoCode = R.iso_code

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
                u.UserProfile.display_name,
                u.UserProfile.country.iso_code,
                u.UserProfile.country_id,
                u.UserProfile.country.name
            }).FirstOrDefaultAsync();

            // No active user with this id (deleted/banned/missing). Return null instead of
            // dereferencing R below, which would throw a NullReferenceException.
            if (R == null)
                return null;

            UserProfileDto response = new UserProfileDto
            {

                DisplayName = R.display_name,
                Bio = R.bio,
                ImageUrl = R.image_url,
                CountryId = R.country_id,
                CountryName = R.name,
                CountryIsoCode = R.iso_code

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
                    ImageUrl = p.image_url,
                    CountryId = p.country_id,
                    CountryName = p.country != null ? p.country.name : null,
                    CountryIsoCode = p.country != null ? p.country.iso_code : null
                })
                .FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateUserAvatarAsync(int userId, string fileName)
        {
            var profile = await context.UsersProfile.FirstOrDefaultAsync(p => p.user_id == userId);
            if (profile == null) return false;

            profile.image_url = fileName;
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<UserProfileEntity> AddUpdateUserProfileByUserIdAsync(int UserId, UserProfileEntity NewUserProfileData)
        {

            // 1. Fetch the existing entity from the database
            var userProfileE = await context.UsersProfile
                .Include(up => up.country) // Eager load the country if you need to return it
                .FirstOrDefaultAsync(up => up.user_id == UserId);

            //if i deleted this check i will be able to update the profile of a user that does not have a profile yet (so i will create it for them)
            if (userProfileE == null)
            {
                userProfileE = new UserProfileEntity
                {
                    user_id = UserId
                };
                context.UsersProfile.Add(userProfileE);
            }

            // 2. Update properties
            userProfileE.bio = NewUserProfileData.bio;
            userProfileE.image_url = NewUserProfileData.image_url;
            userProfileE.country_id = NewUserProfileData.country_id;
            userProfileE.display_name = NewUserProfileData.display_name;

            // 3. Save changes
            // EF automatically detects the changes made to the properties above
            int results = await context.SaveChangesAsync();

            // 4. Return the updated tracked entity
            return userProfileE;
        }

        //user utitlity
        public async Task<bool> IsEmailUsedAsync(string email)
        {

            return await context.Users.AnyAsync(e => e.email == email);

        }
        public async Task<bool> IsUserActiveAsync(int userId)
        {
            return await context.Users.AnyAsync(e => e.user_id == userId && e.status == "active");
        }
        public async Task<bool> DoesUserExistByIdAsync(int userId)
        {
            return await context.Users.AnyAsync(e => e.user_id == userId && e.status != "deleted");
        }

        //custom elemnts
        public async Task<string?> GetHashedPasswordByEmailAsync(string email)
        {

            string? hasshedPassword = await context.Users.Where(u => u.email == email).Select(u => u.hashed_password).FirstOrDefaultAsync();

            return hasshedPassword;

        }
        public async Task<bool> PromotUserToInstructorAsync(int userId, string newStatus)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.user_id == userId);
            if (user == null) return false;
            user.status = newStatus;
            await context.SaveChangesAsync();
            return true;
        }
        public async Task<string?> GetHashedPasswordByIdAsync(int userId)
        {

            string? hashedPassword = await context.Users.Where(u => u.user_id == userId).Select(u => u.hashed_password).FirstOrDefaultAsync();

            return hashedPassword;
        }

        public async Task<int?> GetUserIdByEmail(string email)
        {

            int id = await context.Users.Where(u => u.email == email).Select(u => u.user_id).FirstOrDefaultAsync();

            return id;
        }
    }
}
