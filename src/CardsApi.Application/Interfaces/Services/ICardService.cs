using CardsApi.Application.Dtos;

namespace CardsApi.Application.Interfaces.Services;

public interface ICardService
{
    Task<PagedResultDto<CardResponseDto>> ListAsync(
        Guid userId, int page, DateOnly? expirationFrom, DateOnly? expirationTo, CancellationToken ct);

    Task<CardResponseDto> GetByIdAsync(Guid userId, Guid cardId, CancellationToken ct);

    Task<CardResponseDto> CreateAsync(Guid userId, CardCreateDto dto, CancellationToken ct);

    Task<CardResponseDto> ReplaceAsync(Guid userId, Guid cardId, CardReplaceDto dto, CancellationToken ct);

    Task<CardResponseDto> PatchAsync(Guid userId, Guid cardId, CardPatchDto dto, CancellationToken ct);

    Task<DeleteResultDto> DeleteAsync(Guid userId, Guid cardId, CancellationToken ct);

    Task<CardPinResponseDto> GetPinAsync(Guid userId, Guid cardId, string? requestIp, CancellationToken ct);
}
