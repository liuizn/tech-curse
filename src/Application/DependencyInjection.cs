using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using TechCurse.Application.Factory;
using TechCurse.Application.Interfaces;
using TechCurse.Application.Strategies;
using TechCurse.Application.Common.Behaviors;

namespace TechCurse.Application
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
