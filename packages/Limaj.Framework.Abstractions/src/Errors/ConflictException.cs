namespace Limaj.Framework.Abstractions.Errors;

public class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
