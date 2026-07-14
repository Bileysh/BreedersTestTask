using BreedersTestTask.Domain.Entities;
using BreedersTestTask.Domain.Enums;

namespace BreedersTestTask.Infrastructure;

public static class SeedData
{
    public const int SeededBreederId = 1;
    public const int OtherBreederId = 2;

    public static void EnsureSeeded(BreedersDbContext dbContext)
    {
        if (dbContext.BreederBenefits.Any())
        {
            return;
        }

        dbContext.BreederBenefits.Add(new BreederBenefit
        {
            BreederId = SeededBreederId,
            FreeLimit = 3,
            UsedCount = 0
        });
        
        dbContext.Litters.AddRange(
            new Litter { BreederId = SeededBreederId, Status = LitterStatus.Approved, CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new Litter { BreederId = SeededBreederId, Status = LitterStatus.Draft, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new Litter { BreederId = SeededBreederId, Status = LitterStatus.Published, CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new Litter { BreederId = OtherBreederId, Status = LitterStatus.Approved, CreatedAt = DateTime.UtcNow.AddDays(-1) }
        );

        dbContext.SaveChanges();
    }
}