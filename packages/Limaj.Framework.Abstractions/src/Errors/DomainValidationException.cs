namespace Limaj.Framework.Abstractions.Errors;

public class DomainValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public DomainValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public DomainValidationException(string message, params string[] details)
        : base(string.Format(message, details))
    {
        Errors = new Dictionary<string, string[]>();
    }

    public DomainValidationException(Dictionary<string, string[]> errors, string? message = null)
        : base(message ?? "One or more validation errors occurred.")
    {
        Errors = errors;
    }
}
