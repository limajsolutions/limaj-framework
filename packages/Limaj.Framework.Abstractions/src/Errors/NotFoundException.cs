namespace Limaj.Framework.Abstractions.Errors;

public class NotFoundException : Exception
{
    public string Resource { get; }
    public object? Key { get; }

    public NotFoundException(string resource, object? key = null, string? message = null)
        : base(message ?? $"{resource} was not found.")
    {
        Resource = resource;
        Key = key;
    }
}
