namespace BreedersTestTask.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }

    public int EntityId { get; set; }

    public int BreederId { get; set; }

    public string Action { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}