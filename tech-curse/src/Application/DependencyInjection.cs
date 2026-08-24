using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TechCurse.src.Application.Factory;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Application.Strategies;
using TechCurse.src.Application.Common.Behaviors;

namespace TechCurse.src.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<IPaymentStrategy, CreditCardPaymentStrategy>();
            services.AddScoped<PaymentStrategyFactory>();

            // Configurar MediatR e FluentValidation para a migração CQRS
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            });
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            return services;
        }
    }
}
