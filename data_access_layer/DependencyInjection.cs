

using DataAccess.Data;
using DataAccess.Interfaces;
using DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DataAccess.DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddDataAccessDI(this IServiceCollection service,IConfiguration configuration)
        {

            var ConnectionString = configuration["ConnectionString"];

            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new Exception("connection string is not configured in environment variables");
            }

            // Enable dynamic JSON (System.Text.Json) serialization for jsonb columns
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);
            dataSourceBuilder.EnableDynamicJson();
            var dataSource = dataSourceBuilder.Build();

            service.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(dataSource);

            });

            service.AddScoped<IUserAndProfileRepository, UserAndProfileRepository>();
            service.AddScoped<IAdminActionRepository, AdminActionRepository>();
            service.AddScoped<ILoginLogRepository, LoginLogRepository>();
            service.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            service.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
            service.AddScoped<IReviewRepository, ReviewRepository>();
            service.AddScoped<ICoursesRepository, CoursesRepository>();

            return service;

        }
    }
}
