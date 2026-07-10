using Business.Interfaces;
using Business.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Business.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddBusinessDI(this IServiceCollection service)
        {
            // Scoped to match AppDbContext's lifetime (these services hold the
            // request-scoped context, directly or via their repositories).
            service.AddScoped<IUserService, UserService>();
            service.AddScoped<IRefreshTokenService, RefreshTokenService>();
            service.AddScoped<IAdminActionService, AdminActionService>();
            service.AddScoped<ILoginLogService, LoginLogService>();
            service.AddScoped<IAuthenticationService, AuthenticationService>();
            service.AddScoped<IEnrollmentService, EnrollmentService>();
            service.AddScoped<IReviewService, ReviewService>();
            service.AddScoped<ICourseService, CourseService>();

            return service;
        }
    }
}
