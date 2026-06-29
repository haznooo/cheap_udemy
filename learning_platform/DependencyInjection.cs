
using DataAccess;
using DataAccess.DependencyInjection;

namespace Api
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApiDI(this IServiceCollection service, IConfiguration configuration)
        {
         
            service.AddDataAccessDI(configuration);
            return service;
        }
    }
}
