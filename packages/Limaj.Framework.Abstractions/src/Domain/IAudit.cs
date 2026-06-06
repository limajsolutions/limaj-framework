namespace Limaj.Framework.Abstractions.Domain;

public interface IAudit
{
    bool IsActive { get; }
    DateTime CreatedAt { get; }
    DateTime UpdatedAt { get; }
}
