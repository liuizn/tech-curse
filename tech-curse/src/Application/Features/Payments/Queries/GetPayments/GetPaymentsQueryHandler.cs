using MediatR;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Application.Interfaces;

namespace TechCurse.src.Application.Features.Payments.Queries.GetPayments;

public class GetPaymentsQueryHandler : IRequestHandler<GetPaymentsQuery, PagedResultDto<PaymentOutputDto>>
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly ICacheService _cacheService;
    private const string PAYMENT_LIST_PREFIX = "payments:list:";

    public GetPaymentsQueryHandler(IPaymentRepository paymentRepository, ICacheService cacheService)
    {
        _paymentRepository = paymentRepository;
        _cacheService = cacheService;
    }

    public async Task<PagedResultDto<PaymentOutputDto>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var searchParams = request.SearchParams;
        var cacheKey = $"{PAYMENT_LIST_PREFIX}page:{searchParams.PageNumber}:size:{searchParams.PageSize}:sort:{searchParams.SortBy}_{searchParams.SortDirection}";

        var cachedResult = await _cacheService.GetAsync<PagedResultDto<PaymentOutputDto>>(cacheKey);
        if (cachedResult != null) return cachedResult;

        var (items, totalCount) = await _paymentRepository.GetPagedAsync(searchParams);

        var dtos = items.Select(p => new PaymentOutputDto(
            p.PaymentId, p.EnrollmentId, p.StudentId, p.Amount, p.Status, p.IsActive, p.CreatedAt, p.PaidAt, p.ExternalTransactionId
        ));

        var result = new PagedResultDto<PaymentOutputDto>(dtos, totalCount, searchParams.PageNumber, searchParams.PageSize);
        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15));

        return result;
    }
}
