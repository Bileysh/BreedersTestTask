using BreedersTestTask.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BreedersTestTask.Infrastructure;

public class BreedersDbContext: DbContext
{
    public DbSet<Litter> Litters { get; set; }
    public DbSet<BreederBenefit> BreederBenefits { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    
    public BreedersDbContext(DbContextOptions<BreedersDbContext> options) : base(options){}
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); 
        modelBuilder.Entity<Litter>(entity =>
        {
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(l => l.BreederId);
        });

        modelBuilder.Entity<BreederBenefit>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.HasIndex(b => b.BreederId).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => a.EntityId);
        });
    }
}