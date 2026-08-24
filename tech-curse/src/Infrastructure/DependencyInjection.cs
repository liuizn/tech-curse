using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Infrastructure.Repositories;
using TechCurse.src.Infrastructure.ExternalServices;
using TechCurse.src.Infrastructure.Identity;

namespace TechCurse.src.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
        {
            services.AddScoped<ICourseRepository, CourseRepository>();
            services.AddScoped<IStudentRepository, StudentRepository>();
            services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITokenService, TokenService>();
            services.AddScoped<ICacheService, RedisCacheService>();

            if (environment.IsProduction())
            {
                // Produção
            }
            else
            {
                services.AddScoped<IPaymentGatewayAdapter, SimulatedPaymentGatewayAdapter>();
            }

            return services;
        }
    }
}
