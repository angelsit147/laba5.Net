using Business.Models;

namespace Business.Interfaces;

public interface IIngredientService
{
    Task<IReadOnlyList<IngredientDto>> GetAllAsync(CancellationToken ct = default);
    Task<IngredientDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IngredientDto> CreateAsync(IngredientWriteDto dto, CancellationToken ct = default);
    Task<IngredientDto> UpdateAsync(int id, IngredientWriteDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
