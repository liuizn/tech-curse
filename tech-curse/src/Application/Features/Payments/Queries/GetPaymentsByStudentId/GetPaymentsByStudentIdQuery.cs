using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Payments.Queries.GetPaymentsByStudentId;

public record GetPaymentsByStudentIdQuery(int StudentId, PaginationParamsDto SearchParams) : IRequest<PagedResultDto<PaymentOutputDto>>;
