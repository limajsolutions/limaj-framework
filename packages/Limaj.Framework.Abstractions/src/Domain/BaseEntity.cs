namespace Limaj.Framework.Abstractions.Domain;

public abstract class BaseEntity : IAudit
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
