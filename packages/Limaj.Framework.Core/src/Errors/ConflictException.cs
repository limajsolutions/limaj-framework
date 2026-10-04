namespace Limaj.Framework.Core.Errors;

public class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}
