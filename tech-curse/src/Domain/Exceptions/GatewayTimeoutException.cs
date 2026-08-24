namespace TechCurse.src.Domain.Exceptions;

public class GatewayTimeoutException : Exception
{
    public GatewayTimeoutException(string message) : base(message)
    {
    }
}
