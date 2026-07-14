namespace BreedersTestTask.Services.Interface;

public interface INotificationService
{
    Task NotifyLitterPublishedAsync(int litterId, int breederId, CancellationToken cancellationToken = default);
}