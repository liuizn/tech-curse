using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Payments.Commands.CreatePayment;

public record CreatePaymentCommand(int EnrollmentId, decimal Amount) : IRequest<PaymentOutputDto>;
