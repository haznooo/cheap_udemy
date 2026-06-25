

using DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DataAccess
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

            // Enable dynamic JSON (System.Text.Json) serialization for Npgsql 8+
            // This allows writing arbitrary .NET types (e.g., List<ContentBlock>) to jsonb columns.
            NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();

            service.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(ConnectionString);

            });

            return service;

        }
    }
}
