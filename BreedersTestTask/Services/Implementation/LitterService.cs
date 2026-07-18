using BreedersTestTask.Domain.Entities;
using BreedersTestTask.Domain.Enums;
using BreedersTestTask.DTOs;
using BreedersTestTask.Exceptions;
using BreedersTestTask.Infrastructure;
using BreedersTestTask.Services.Interface;
using Microsoft.EntityFrameworkCore;

namespace BreedersTestTask.Services.Implementation;

public class LitterService : ILitterService
{
    private readonly BreedersDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IBreederContext _breederContext;
    private readonly ILogger<LitterService> _logger;

    public LitterService(
        BreedersDbContext db,
        INotificationService notificationService,
        IBreederContext breederContext,
        ILogger<LitterService> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _breederContext = breederContext;
        _logger = logger;
    }

    public async Task<PagedResult<LitterDto>> GetLittersAsync(
        GetLittersQuery query,
        CancellationToken cancellationToken = default)
    {
        int breederId = _breederContext.BreederId;

        if (query.PageNumber < 1)
        {
            throw new ValidationException("pageNumber must be greater than or equal to 1.");
        }

        if (query.PageSize < 1 || query.PageSize > GetLittersQuery.MaxPageSize)
        {
            throw new ValidationException($"pageSize must be between 1 and {GetLittersQuery.MaxPageSize}.");
        }
        
        int pageNumber = query.PageNumber;
        int pageSize = query.PageSize;
        
        var dbQuery = _db.Litters.AsNoTracking().Where(l => l.BreederId == breederId);

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(l => l.Status == query.Status.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var items = await dbQuery
            .OrderByDescending(l => l.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LitterDto(
                l.Id,
                l.BreederId,
                l.Status.ToString(),
                l.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PagedResult<LitterDto>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<LitterDto> PublishAsync(int litterId, CancellationToken cancellationToken = default)
    {
        int breederId = _breederContext.BreederId;

        var litter = await _db.Litters.FirstOrDefaultAsync(l => l.Id == litterId, cancellationToken)
            ?? throw new NotFoundException($"Litter with id {litterId} was not found.");

        if (litter.BreederId != breederId)
        {
            throw new ForbiddenException("You are not allowed to publish a litter that does not belong to you.");
        }

        if (litter.Status != LitterStatus.Approved)
        {
            throw new DomainException(
                $"Litter must be in '{LitterStatus.Approved}' status to be published. Current status: '{litter.Status}'.");
        }

        var benefitExists = await _db.BreederBenefits.AnyAsync(b => b.BreederId == breederId, cancellationToken);
        if (!benefitExists)
        {
            throw new NotFoundException($"No benefit record found for breeder {breederId}.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var rowsAffected = await _db.BreederBenefits
                .Where(b => b.BreederId == breederId && b.UsedCount < b.FreeLimit)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(b => b.UsedCount, b => b.UsedCount + 1),
                    cancellationToken);
            if (rowsAffected == 0)
            {
            
                _db.AuditLogs.Add(new AuditLog
                {
                    EntityId = litter.Id,
                    BreederId = breederId,
                    Action = "Publish attempt failed - limits exceeded",
                    CreatedAt = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                throw new DomainException("Free publish limit exceeded for this breeder.");
            }

            litter.Status = LitterStatus.Published;

            _db.AuditLogs.Add(new AuditLog
            {
                EntityId = litter.Id,
                BreederId = breederId,
                Action = "Published for free",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DomainException)
        {
          throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit publish transaction for litter {LitterId}", litterId);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await _notificationService.NotifyLitterPublishedAsync(litter.Id, breederId, cancellationToken);

        return new LitterDto(litter.Id, litter.BreederId, litter.Status.ToString(), litter.CreatedAt);
    }
}