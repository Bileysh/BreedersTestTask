using BreedersTestTask.Domain.Enums;

namespace BreedersTestTask.DTOs;

public record LitterDto(
    int Id,
    int BreederId,
    string Status,
    DateTime CreatedAt
);