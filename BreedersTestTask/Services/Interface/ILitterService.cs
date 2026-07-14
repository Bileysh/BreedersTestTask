using BreedersTestTask.DTOs;

namespace BreedersTestTask.Services.Interface;

public interface ILitterService
{
    Task<PagedResult<LitterDto>> GetLittersAsync(GetLittersQuery query, CancellationToken cancellationToken = default);

    Task<LitterDto> PublishAsync(int litterId, CancellationToken cancellationToken = default);
}