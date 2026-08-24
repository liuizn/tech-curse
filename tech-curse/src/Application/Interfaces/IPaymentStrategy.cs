using TechCurse.src.Application.DTOs;
using TechCurse.src.Domain.Entities;
using TechCurse.src.Domain.Enums;

namespace TechCurse.src.Application.Interfaces;

public interface IPaymentStrategy
{
    PaymentMethodType PaymentMethodType { get; }
    Task<GatewayResponse> ProcessAsync(Payment payment, IPaymentGatewayAdapter gateway, string idempotencyKey, CancellationToken cancellationToken);
}
