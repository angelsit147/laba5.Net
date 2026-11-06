using Business.Interfaces;
using Business.Models;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/loyalty-levels")]
public class LoyaltyLevelsController : ControllerBase
{
    private readonly ILoyaltyService service;

    public LoyaltyLevelsController(ILoyaltyService service)
    {
        this.service = service;
    }

    [HttpGet]
    [ProducesResponseType(typeof(LoyaltyLevelDto[]), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<LoyaltyLevelDto>> GetAll(CancellationToken ct) => service.GetAllAsync(ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(LoyaltyLevelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<LoyaltyLevelDto> GetById(int id, CancellationToken ct) => service.GetByIdAsync(id, ct);

    [HttpPut]
    [ProducesResponseType(typeof(LoyaltyLevelDto[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<IReadOnlyList<LoyaltyLevelDto>> Update(UpdateLoyaltyLevelsDto dto, CancellationToken ct) =>
        service.UpdateLevelsAsync(dto, ct);
}
