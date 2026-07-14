using BreedersTestTask.Domain.Enums;

namespace BreedersTestTask.Domain.Entities;

public class Litter
{
    public int Id { get; set; }

    public int BreederId { get; set; }

    public LitterStatus Status { get; set; } = LitterStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
