using TechCurse.src.Application.DTOs;
using TechCurse.src.Application.Interfaces;

namespace TechCurse.src.Infrastructure.ExternalServices;

public class SimulatedPaymentGatewayAdapter : IPaymentGatewayAdapter
{
    public async Task<GatewayResponse> CreateTransactionAsync(decimal amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        // Simulação de Timeout da API externa
        await Task.Delay(500, cancellationToken);

        // Respostas Determinísticas baseadas no valor para facilitar testes
        if (amount == 999.99m)
        {
            // Simula recusa por fraude
            return MappedErrorResponse("EXT_01", "Transação recusada pelo sistema antifraude.");
        }

        if (amount == 500.00m)
        {
            // Simula saldo insuficiente
            return MappedErrorResponse("EXT_02", "Saldo insuficiente no método de pagamento.");
        }

        // Sucesso
        return new GatewayResponse(
            IsSuccess: true,
            TransactionId: $"sim_{Guid.NewGuid():N}",
            ReceiptUrl: $"https://simulated-gateway.com/receipts/{idempotencyKey}",
            ErrorCode: null,
            ErrorMessage: null,
            ProcessedAt: DateTime.UtcNow
        );
    }

    public async Task<GatewayResponse> ConfirmTransactionAsync(string externalId, CancellationToken cancellationToken)
    {
        await Task.Delay(200, cancellationToken);
        return new GatewayResponse(true, externalId, null, null, null, DateTime.UtcNow);
    }

    public async Task<GatewayResponse> RefundTransactionAsync(string externalId, CancellationToken cancellationToken, string idempotencyKey)
    {
        await Task.Delay(300, cancellationToken);
        return new GatewayResponse(true, externalId, null, null, null, DateTime.UtcNow);
    }

    // Mapeamento interno de erros externos
    private GatewayResponse MappedErrorResponse(string rawErrorCode, string rawMessage)
    {
        // Aqui você mapearia os códigos exóticos da API externa para um padrão da sua API
        var internalCode = rawErrorCode switch
        {
            "EXT_01" => "FRAUD_DETECTED",
            "EXT_02" => "INSUFFICIENT_FUNDS",
            _ => "UNKNOWN_GATEWAY_ERROR"
        };

        return new GatewayResponse(false, null, null, internalCode, rawMessage, DateTime.UtcNow);
    }
}
