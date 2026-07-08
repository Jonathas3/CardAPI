using CardsApi.Domain.Entities;

namespace CardsApi.Application.Interfaces.Repositories;

public record CardListResult(IReadOnlyList<Card> Items, int TotalCount);
