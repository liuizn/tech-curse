using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Payments.Queries.GetPaymentsByEnrollmentId;

public record GetPaymentsByEnrollmentIdQuery(int EnrollmentId) : IRequest<IEnumerable<PaymentOutputDto>>;
