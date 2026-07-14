using BreedersTestTask.Services.Interface;

namespace BreedersTestTask.Services.Implementation;

public class ConsoleNotificationService: INotificationService
{
    private readonly ILogger<ConsoleNotificationService> _logger;

    public ConsoleNotificationService(ILogger<ConsoleNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifyLitterPublishedAsync(int litterId, int breederId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Notification: litter {LitterId} for breeder {BreederId} has been published.", litterId, breederId);
        
        Console.WriteLine($"[Notification] Litter {litterId} for breeder {breederId} has been published.");
        
        return Task.CompletedTask;
    }
}