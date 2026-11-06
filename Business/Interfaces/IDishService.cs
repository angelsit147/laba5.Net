using Business.Models;

namespace Business.Interfaces;

public interface IDishService
{
    Task<IReadOnlyList<DishDto>> GetAllAsync(CancellationToken ct = default);
    Task<DishDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<DishDto> CreateAsync(DishWriteDto dto, CancellationToken ct = default);
    Task<DishDto> UpdateAsync(int id, DishWriteDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
