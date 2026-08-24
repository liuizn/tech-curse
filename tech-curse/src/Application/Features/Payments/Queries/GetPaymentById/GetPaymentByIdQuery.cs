using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Payments.Queries.GetPaymentById;

public record GetPaymentByIdQuery(int Id) : IRequest<PaymentOutputDto>;
