using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Payments.Commands.RefundPayment;

public record RefundPaymentCommand(int PaymentId, string IdempotencyKey) : IRequest<RefundPaymentOutputDto>;
