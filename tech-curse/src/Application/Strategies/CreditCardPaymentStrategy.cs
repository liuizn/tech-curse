using TechCurse.src.Application.DTOs;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Domain.Entities;
using TechCurse.src.Domain.Enums;

namespace TechCurse.src.Application.Strategies;

public class CreditCardPaymentStrategy : IPaymentStrategy
{
    public PaymentMethodType PaymentMethodType => PaymentMethodType.CreditCard;

    public async Task<GatewayResponse> ProcessAsync(Payment payment, IPaymentGatewayAdapter gateway, string idempotencyKey, CancellationToken cancellationToken)
    {
        // Para cartão, geralmente chamamos o Create que já debita o valor
        return await gateway.CreateTransactionAsync(payment.Amount, idempotencyKey, cancellationToken);
    }
}
