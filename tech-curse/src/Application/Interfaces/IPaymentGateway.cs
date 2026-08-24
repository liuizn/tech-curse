using TechCurse.src.Application.DTOs;
using TechCurse.src.Domain.Entities;

namespace TechCurse.src.Application.Interfaces;

public interface IPaymentGatewayAdapter
{
    Task<GatewayResponse> CreateTransactionAsync(decimal amount, string idempotencyKey, CancellationToken cancellationToken);
    Task<GatewayResponse> ConfirmTransactionAsync(string ExternalTransactionId, CancellationToken cancellationToken);
    Task<GatewayResponse> RefundTransactionAsync(string ExternalTransactionId, CancellationToken cancellationToken, string idempotencyKey);
}
