using Cards.Application.Dtos;
using Cards.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cards.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cards")]
public class CardsController : ControllerBase
{
    private readonly ICardService _cardService;

    public CardsController(ICardService cardService)
    {
        _cardService = cardService;
    }

    /// <summary>Lista os cartões do usuário autenticado, do mais recente para o mais antigo, em blocos de 10.</summary>
    /// <remarks>Opcionalmente restringe os resultados por um intervalo de vencimento do cartão.</remarks>
    /// <param name="page">Número da página, a partir de 1 (padrão 1).</param>
    /// <param name="expirationFrom">Limite inferior (inclusive) de expirationDate (yyyy-MM-dd).</param>
    /// <param name="expirationTo">Limite superior (inclusive) de expirationDate (yyyy-MM-dd).</param>
    /// <param name="ct">Token de cancelamento.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<CardResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<CardResponseDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] DateOnly? expirationFrom = null,
        [FromQuery] DateOnly? expirationTo = null,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var result = await _cardService.ListAsync(userId, page, expirationFrom, expirationTo, ct);
        return Ok(result);
    }

    /// <summary>Consulta um cartão específico do usuário autenticado, por id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CardResponseDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var card = await _cardService.GetByIdAsync(userId, id, ct);
        return Ok(card);
    }

    /// <summary>Cria um novo cartão para o usuário autenticado.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CardResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CardResponseDto>> Create([FromBody] CardCreateDto dto, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var card = await _cardService.CreateAsync(userId, dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = card.Id }, card);
    }

    /// <summary>Atualização completa. Todos os campos editáveis devem ser enviados; ver README para detalhes.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CardResponseDto>> Replace(Guid id, [FromBody] CardReplaceDto dto, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var card = await _cardService.ReplaceAsync(userId, id, dto, ct);
        return Ok(card);
    }

    /// <summary>Atualização parcial. Só os campos enviados no corpo são alterados.</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(CardResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CardResponseDto>> Patch(Guid id, [FromBody] CardPatchDto dto, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var card = await _cardService.PatchAsync(userId, id, dto, ct);
        return Ok(card);
    }

    /// <summary>Remove (soft delete) um cartão: some das consultas comuns, mas é preservado para rastreabilidade.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(DeleteResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeleteResultDto>> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await _cardService.DeleteAsync(userId, id, ct);
        return Ok(result);
    }

    /// <summary>Consulta a senha (PIN) original do cartão, em um endpoint exclusivo e auditado.</summary>
    /// <remarks>
    /// Separado das consultas comuns de cartão para que listagens/detalhes nunca carreguem
    /// o PIN, e para que cada acesso possa ser autorizado e auditado individualmente.
    /// </remarks>
    [HttpGet("{id:guid}/pin")]
    [ProducesResponseType(typeof(CardPinResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CardPinResponseDto>> GetPin(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _cardService.GetPinAsync(userId, id, ip, ct);
        return Ok(result);
    }
}
