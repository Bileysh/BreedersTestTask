using BreedersTestTask.Domain.Enums;

namespace BreedersTestTask.DTOs;

public record GetLittersQuery(
    LitterStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 10 
)
{
    public const int MaxPageSize = 100;
}