using TechCurse.src.Application.Interfaces;
using TechCurse.src.Domain.Enums;

namespace TechCurse.src.Application.Factory;

public class PaymentStrategyFactory
{
    private readonly IEnumerable<IPaymentStrategy> _strategies;

    public PaymentStrategyFactory(IEnumerable<IPaymentStrategy> strategies)
    {
        _strategies = strategies;
    }

    public IPaymentStrategy GetStrategy(PaymentMethodType methodType)
    {
        var strategy = _strategies.FirstOrDefault(s => s.PaymentMethodType == methodType);
        if (strategy == null)
            throw new NotSupportedException($"Método de pagamento {methodType} não suportado.");

        return strategy;
    }
}
