namespace TechCurse.Domain.Exceptions;

public class BadRequestExecption : Exception
{
    public BadRequestExecption(string message) : base(message)
    {
    }
}
