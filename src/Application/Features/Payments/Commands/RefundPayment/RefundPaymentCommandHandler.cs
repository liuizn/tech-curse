using MediatR;
using Microsoft.Extensions.Logging;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Payments.Commands.RefundPayment;

public class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, RefundPaymentOutputDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IPaymentGatewayAdapter _paymentGateway;
    private readonly ICacheService _cacheService;
    private readonly ILogger<RefundPaymentCommandHandler> _logger;

    private const string PAYMENT_LIST_PREFIX = "payments:list:";
    private const string PAYMENT_ITEM_PREFIX = "payments:item:";
    private const string PAYMENT_BY_STUDENT_PREFIX = "payments:student:";
    private const string PAYMENT_BY_ENROLLMENT_PREFIX = "payments:enrollment:";

    public RefundPaymentCommandHandler(
        IPaymentRepository paymentRepository, 
        IPaymentGatewayAdapter paymentGateway, 
        ICacheService cacheService, 
        ILogger<RefundPaymentCommandHandler> logger)
    {
        _paymentRepository = paymentRepository;
        _paymentGateway = paymentGateway;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<RefundPaymentOutputDto> Handle(RefundPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _paymentRepository.GetByIdAsync(request.PaymentId);
        if (payment == null) throw new NotFoundException("Pagamento não encontrado.");

        if (payment.Status != PaymentStatus.Paid || string.IsNullOrEmpty(payment.ExternalTransactionId))
            throw new NotAllowedException("Apenas pagamentos processados e com ID de transação podem ser estornados.");

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var gatewayResult = await _paymentGateway.RefundTransactionAsync(
                payment.ExternalTransactionId,
                cts.Token,
                request.IdempotencyKey
            );

            if (!gatewayResult.IsSuccess)
            {
                _logger.LogWarning("Falha no gateway ao estornar PaymentId {PaymentId}. Erro: {ErrorCode} - {ErrorMessage}",
                    payment.PaymentId, gatewayResult.ErrorCode, gatewayResult.ErrorMessage);
                throw new BadRequestExecption($"Falha ao estornar pagamento [{gatewayResult.ErrorCode}]: {gatewayResult.ErrorMessage}");
            }

            payment.Status = PaymentStatus.Refunded;
            payment.IsActive = false;
            payment.RefundedAt = DateTime.UtcNow;

            await _paymentRepository.UpdateAsync(payment);
            await ClearPaymentCachesAsync();

            _logger.LogInformation("Estorno realizado com sucesso para o PaymentId {PaymentId}. TransactionId: {TransactionId}",
                payment.PaymentId, payment.ExternalTransactionId);

            return new RefundPaymentOutputDto(true, "Estorno realizado com sucesso.");
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Timeout na comunicação com o gateway durante o estorno do PaymentId {PaymentId}", request.PaymentId);
            throw new GatewayTimeoutException("A comunicação com o provedor de pagamento excedeu o tempo limite durante o estorno.");
        }
    }

    private async Task ClearPaymentCachesAsync()
    {
        await _cacheService.RemoveByPrefixAsync(PAYMENT_LIST_PREFIX);
        await _cacheService.RemoveByPrefixAsync(PAYMENT_ITEM_PREFIX);
        await _cacheService.RemoveByPrefixAsync(PAYMENT_BY_STUDENT_PREFIX);
        await _cacheService.RemoveByPrefixAsync(PAYMENT_BY_ENROLLMENT_PREFIX);
    }
}
