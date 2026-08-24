using MediatR;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Domain.Enums;
using TechCurse.src.Domain.Exceptions;

namespace TechCurse.src.Application.Features.Payments.Queries.GetPaymentById;

public class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, PaymentOutputDto>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICacheService _cacheService;
    private readonly ICurrentUserService _currentUserService;
    private const string PAYMENT_ITEM_PREFIX = "payments:item:";

    public GetPaymentByIdQueryHandler(IPaymentRepository paymentRepository, ICacheService cacheService, ICurrentUserService currentUserService)
    {
        _paymentRepository = paymentRepository;
        _cacheService = cacheService;
        _currentUserService = currentUserService;
    }

    public async Task<PaymentOutputDto> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"{PAYMENT_ITEM_PREFIX}{request.Id}";
        
        var payment = await _paymentRepository.GetByIdAsync(request.Id);
        if (payment == null) throw new NotFoundException("Pagamento não encontrado.");

        await ValidateRoleAcess(payment.Student.IdentityUserId);

        var cached = await _cacheService.GetAsync<PaymentOutputDto>(cacheKey);
        if (cached != null) return cached;

        var dto = new PaymentOutputDto(
            payment.PaymentId, payment.EnrollmentId, payment.StudentId, payment.Amount, payment.Status, 
            payment.IsActive, payment.CreatedAt, payment.PaidAt, payment.ExternalTransactionId
        );

        await _cacheService.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(15));
        return dto;
    }

    private Task ValidateRoleAcess(string targetIdentityUserId)
    {
        var currentUserId = _currentUserService.GetUserId();
        var isAdmin = _currentUserService.IsInRole(UserRole.Admin);

        if (currentUserId != targetIdentityUserId && !isAdmin)
            throw new NotAllowedException("Você não possuí permissão suficiente para acessar este registro!");

        return Task.CompletedTask;
    }
}
