using MediatR;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Domain.Enums;

namespace TechCurse.src.Application.Features.Payments.Commands.ProcessPayment;

public record ProcessPaymentCommand(int PaymentId, PaymentMethodType Type, string IdempotencyKey) : IRequest<ProcessPaymentOutputDto>;
