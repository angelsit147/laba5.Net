using Business.Models;

namespace Business.Interfaces;

public interface ICustomerService
{
    Task<CustomerRegistrationResultDto> RegisterAsync(CustomerRegistrationDto dto, CancellationToken ct = default);
    Task<LoyaltyCardDto> ResolveLoyaltyAsync(LoyaltyCodeDto dto, CancellationToken ct = default);
}
