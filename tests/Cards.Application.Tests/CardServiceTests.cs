using System.Net;
using Cards.Application.Common;
using Cards.Application.Dtos;
using Cards.Application.Interfaces.Repositories;
using Cards.Application.Interfaces.Services;
using Cards.Application.Services;
using Cards.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cards.Application.Tests;

public class CardServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ListAsync_UsesFixedPageSizeAndReturnsMaskedCards()
    {
        var repository = new FakeCardRepository();
        repository.Cards.AddRange(Enumerable.Range(1, 12).Select(i => NewCard(i)));
        var service = NewService(repository);

        var result = await service.ListAsync(UserId, page: 1, null, null, CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(12, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(10, result.Items.Count);
        Assert.All(result.Items, item => Assert.Contains("****", item.MaskedNumber));
        Assert.Equal(repository.LastPageSize, result.PageSize);
    }

    [Fact]
    public async Task ListAsync_RejectsInvalidExpirationRange()
    {
        var service = NewService(new FakeCardRepository());

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.ListAsync(
                UserId,
                page: 1,
                expirationFrom: new DateOnly(2030, 1, 1),
                expirationTo: new DateOnly(2029, 1, 1),
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFoundWhenCardIsNotOwnedByUser()
    {
        var repository = new FakeCardRepository();
        repository.Cards.Add(NewCard(1, userId: Guid.Parse("22222222-2222-2222-2222-222222222222")));
        var service = NewService(repository);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.GetByIdAsync(UserId, repository.Cards[0].Id, CancellationToken.None));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_EncryptsSensitiveFieldsAndReturnsOnlyMaskedNumber()
    {
        var repository = new FakeCardRepository();
        var crypto = new FakeCryptoService();
        var service = NewService(repository, crypto);

        var result = await service.CreateAsync(UserId, new CardCreateDto
        {
            CardholderName = "MARIANA ALVES",
            Nickname = "Principal",
            Brand = "visa",
            CardNumber = "5321123412345336",
            ExpirationDate = new DateOnly(2029, 12, 31),
            CreditLimit = 6500,
            Pin = "1234"
        }, CancellationToken.None);

        var saved = Assert.Single(repository.Cards);
        Assert.Equal("enc:5321123412345336", saved.CardNumberEncrypted);
        Assert.Equal("enc:1234", saved.PinEncrypted);
        Assert.Equal("5321 **** **** 5336", result.MaskedNumber);
        Assert.DoesNotContain("1234", result.MaskedNumber);
    }

    [Fact]
    public async Task ReplaceAsync_UpdatesAllEditableFieldsAndReEncryptsSensitiveData()
    {
        var repository = new FakeCardRepository();
        var card = NewCard(1);
        repository.Cards.Add(card);
        var service = NewService(repository);

        var result = await service.ReplaceAsync(UserId, card.Id, new CardReplaceDto
        {
            CardholderName = "MARIANA ALVES",
            Nickname = "Principal Atualizado",
            Brand = "visa",
            CardNumber = "4111111111111111",
            ExpirationDate = new DateOnly(2030, 1, 31),
            CreditLimit = 9999,
            Status = "BLOCKED",
            Pin = "9876"
        }, CancellationToken.None);

        Assert.Equal("Principal Atualizado", result.Nickname);
        Assert.Equal("4111 **** **** 1111", result.MaskedNumber);
        Assert.Equal("BLOCKED", result.Status);
        Assert.Equal("enc:4111111111111111", card.CardNumberEncrypted);
        Assert.Equal("enc:9876", card.PinEncrypted);
    }

    [Fact]
    public async Task ReplaceAsync_RejectsInvalidStatus()
    {
        var repository = new FakeCardRepository();
        var card = NewCard(1);
        repository.Cards.Add(card);
        var service = NewService(repository);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.ReplaceAsync(UserId, card.Id, new CardReplaceDto
            {
                CardholderName = "MARIANA ALVES",
                Nickname = "Principal",
                Brand = "VISA",
                CardNumber = "4111111111111111",
                ExpirationDate = new DateOnly(2030, 1, 31),
                CreditLimit = 1000,
                Status = "INVALID",
                Pin = "9876"
            }, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task PatchAsync_UpdatesOnlySuppliedFields()
    {
        var repository = new FakeCardRepository();
        var card = NewCard(1);
        repository.Cards.Add(card);
        var service = NewService(repository);

        var result = await service.PatchAsync(UserId, card.Id, new CardPatchDto
        {
            Nickname = "Uso diario",
            CreditLimit = 15500
        }, CancellationToken.None);

        Assert.Equal("Uso diario", result.Nickname);
        Assert.Equal(15500, result.CreditLimit);
        Assert.Equal("VISA", result.Brand);
        Assert.Equal("enc:5321123412340001", card.CardNumberEncrypted);
    }

    [Fact]
    public async Task PatchAsync_RejectsInvalidStatus()
    {
        var repository = new FakeCardRepository();
        var card = NewCard(1);
        repository.Cards.Add(card);
        var service = NewService(repository);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.PatchAsync(UserId, card.Id, new CardPatchDto { Status = "INVALID" }, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task PatchAsync_RejectsInvalidCardNumberFormat()
    {
        var repository = new FakeCardRepository();
        var card = NewCard(1);
        repository.Cards.Add(card);
        var service = NewService(repository);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            service.PatchAsync(UserId, card.Id, new CardPatchDto { CardNumber = "abc" }, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesCardSoItDisappearsFromFutureLookups()
    {
        var repository = new FakeCardRepository();
        var card = NewCard(1);
        repository.Cards.Add(card);
        var service = NewService(repository);

        var result = await service.DeleteAsync(UserId, card.Id, CancellationToken.None);

        Assert.True(result.Deleted);
        Assert.True(card.IsDeleted);
        Assert.NotNull(card.DeletedAt);

        await Assert.ThrowsAsync<ApiException>(() =>
            service.GetByIdAsync(UserId, card.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetPinAsync_DecryptsPinOnlyThroughExclusiveFlowAndAuditsAccess()
    {
        var repository = new FakeCardRepository();
        var card = NewCard(1);
        card.PinEncrypted = "enc:9876";
        repository.Cards.Add(card);
        var service = NewService(repository, new FakeCryptoService());

        var result = await service.GetPinAsync(UserId, card.Id, "127.0.0.1", CancellationToken.None);

        Assert.Equal("9876", result.Pin);
        var log = Assert.Single(repository.PinAccessLogs);
        Assert.Equal(card.Id, log.CardId);
        Assert.Equal(UserId, log.UserId);
        Assert.Equal("127.0.0.1", log.Ip);
        Assert.Equal(1, repository.SaveChangesCalls);
    }

    private static CardService NewService(
        FakeCardRepository repository,
        ICryptoService? crypto = null)
    {
        return new CardService(
            repository,
            crypto ?? new FakeCryptoService(),
            NullLogger<CardService>.Instance);
    }

    private static Card NewCard(int index, Guid? userId = null)
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            UserId = userId ?? UserId,
            CardholderName = "MARIANA ALVES",
            Nickname = $"Card {index}",
            Brand = "VISA",
            CardNumberEncrypted = $"enc:532112341234{index:0000}",
            CardNumberFirst4 = "5321",
            CardNumberLast4 = index.ToString("0000"),
            PinEncrypted = "enc:1234",
            ExpirationDate = new DateOnly(2029, 12, 31),
            CreditLimit = 1000 + index,
            Status = CardStatus.Active,
            CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(index),
            UpdatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(index)
        };
        return card;
    }

    private sealed class FakeCryptoService : ICryptoService
    {
        public string Encrypt(string plainText) => $"enc:{plainText}";

        public string Decrypt(string cipherTextBase64) =>
            cipherTextBase64.StartsWith("enc:", StringComparison.Ordinal)
                ? cipherTextBase64[4..]
                : cipherTextBase64;
    }

    private sealed class FakeCardRepository : ICardRepository
    {
        public List<Card> Cards { get; } = new();
        public List<PinAccessLog> PinAccessLogs { get; } = new();
        public int? LastPageSize { get; private set; }
        public int SaveChangesCalls { get; private set; }

        public Task<CardListResult> ListAsync(
            Guid userId,
            DateOnly? expirationFrom,
            DateOnly? expirationTo,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            LastPageSize = pageSize;

            var query = Cards
                .Where(c => c.UserId == userId && !c.IsDeleted)
                .AsEnumerable();

            if (expirationFrom is not null)
            {
                query = query.Where(c => c.ExpirationDate >= expirationFrom.Value);
            }

            if (expirationTo is not null)
            {
                query = query.Where(c => c.ExpirationDate <= expirationTo.Value);
            }

            var ordered = query.OrderByDescending(c => c.CreatedAt).ToList();
            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new CardListResult(items, ordered.Count));
        }

        public Task<Card?> FindByIdAsync(Guid userId, Guid cardId, CancellationToken ct)
        {
            var card = Cards.FirstOrDefault(c => c.Id == cardId && c.UserId == userId && !c.IsDeleted);
            return Task.FromResult(card);
        }

        public Task AddAsync(Card card, CancellationToken ct)
        {
            Cards.Add(card);
            return Task.CompletedTask;
        }

        public Task AddPinAccessLogAsync(PinAccessLog log, CancellationToken ct)
        {
            PinAccessLogs.Add(log);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveChangesCalls++;
            return Task.CompletedTask;
        }
    }
}
