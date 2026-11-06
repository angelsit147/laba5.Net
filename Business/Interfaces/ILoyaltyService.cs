using Business.Models;
using Data.Entities;

namespace Business.Interfaces;

public interface ILoyaltyService
{
    Task<IReadOnlyList<LoyaltyLevelDto>> GetAllAsync(CancellationToken ct = default);
    Task<LoyaltyLevelDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<LoyaltyLevelDto>> UpdateLevelsAsync(UpdateLoyaltyLevelsDto dto, CancellationToken ct = default);


    Task RecalculateCustomerAsync(int customerId, Order completedOrder, CancellationToken ct = default);
}
