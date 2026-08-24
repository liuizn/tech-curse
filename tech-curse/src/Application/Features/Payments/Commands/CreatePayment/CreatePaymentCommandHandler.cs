using MediatR;
using Microsoft.Extensions.Logging;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Domain.Entities;
using TechCurse.src.Domain.Enums;
using TechCurse.src.Domain.Exceptions;

namespace TechCurse.src.Application.Features.Payments.Commands.CreatePayment;

public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentOutputDto>
{
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CreatePaymentCommandHandler> _logger;

    private const string PAYMENT_LIST_PREFIX = "payments:list:";
    private const string PAYMENT_ITEM_PREFIX = "payments:item:";
    private const string PAYMENT_BY_STUDENT_PREFIX = "payments:student:";
    private const string PAYMENT_BY_ENROLLMENT_PREFIX = "payments:enrollment:";

    public CreatePaymentCommandHandler(IEnrollmentRepository enrollmentRepository, IPaymentRepository paymentRepository, ICacheService cacheService, ILogger<CreatePaymentCommandHandler> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _paymentRepository = paymentRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<PaymentOutputDto> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        Enrollment? enrollment = await _enrollmentRepository.GetByIdAsync(request.EnrollmentId);
        if (enrollment == null)
        {
            _logger.LogWarning("Tentativa de criar pagamento para matrícula inexistente: {EnrollmentId}", request.EnrollmentId);
            throw new NotFoundException("Matrícula não encontrada.");
        }

        var isEnrollmentActive = await _enrollmentRepository.EnrollmentIsActiveAsync(request.EnrollmentId);
        if (!isEnrollmentActive)
        {
            _logger.LogWarning("Falha ao criar intenção. Matrícula inativa: {EnrollmentId}", request.EnrollmentId);
            throw new NotAllowedException("Não é possível criar um pagamento para uma matrícula inativa.");
        }

        var paymentExists = await _paymentRepository.ExistsActiveByEnrollmentAsync(request.EnrollmentId);
        if (paymentExists)
        {
            throw new ConflictException("Já existe um pagamento ativo para esta matrícula.");
        }

        Payment newPayment = new Payment
        {
            EnrollmentId = request.EnrollmentId,
            StudentId = enrollment.StudentId,
            Amount = request.Amount,
            Status = PaymentStatus.Pending,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _paymentRepository.AddAsync(newPayment);
        await ClearPaymentCachesAsync();

        _logger.LogInformation("Intenção de pagamento criada com sucesso. PaymentId: {PaymentId}, EnrollmentId: {EnrollmentId}",
            newPayment.PaymentId, newPayment.EnrollmentId);

        return new PaymentOutputDto(
            newPayment.PaymentId, newPayment.EnrollmentId, newPayment.StudentId, newPayment.Amount,
            newPayment.Status, newPayment.IsActive, newPayment.CreatedAt, null, null
        );
    }

    private async Task ClearPaymentCachesAsync()
    {
        await _cacheService.RemoveByPrefixAsync(PAYMENT_LIST_PREFIX);
        await _cacheService.RemoveByPrefixAsync(PAYMENT_ITEM_PREFIX);
        await _cacheService.RemoveByPrefixAsync(PAYMENT_BY_STUDENT_PREFIX);
        await _cacheService.RemoveByPrefixAsync(PAYMENT_BY_ENROLLMENT_PREFIX);
    }
}
