using Cards.Domain.Entities;

namespace Cards.Application.Interfaces.Repositories;

public record CardListResult(IReadOnlyList<Card> Items, int TotalCount);
