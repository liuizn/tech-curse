using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Payments.Queries.GetPayments;

public record GetPaymentsQuery(PaginationParamsDto SearchParams) : IRequest<PagedResultDto<PaymentOutputDto>>;
