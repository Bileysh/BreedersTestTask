namespace BreedersTestTask.Domain.Entities;

public class BreederBenefit
{
    public int Id { get; set; }

    public int BreederId { get; set; }

    public int FreeLimit { get; set; }

    public int UsedCount { get; set; }
}
