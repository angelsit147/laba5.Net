using Business.Interfaces;
using Business.Models;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService service;

    public CustomersController(ICustomerService service)
    {
        this.service = service;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerRegistrationResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CustomerRegistrationResultDto>> Register(
        CustomerRegistrationDto dto, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await service.RegisterAsync(dto, ct));

    [HttpPost("resolve-loyalty")]
    [ProducesResponseType(typeof(LoyaltyCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<LoyaltyCardDto> ResolveLoyalty(LoyaltyCodeDto dto, CancellationToken ct) =>
        service.ResolveLoyaltyAsync(dto, ct);
}
