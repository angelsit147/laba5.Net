using Business.Models;

namespace Business.Interfaces;

public interface IMenuService
{
    Task<IReadOnlyList<MenuItemDto>> GetAllAsync(bool availableOnly = true, CancellationToken ct = default);
    Task<MenuItemDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<MenuItemDto> CreateAsync(MenuItemWriteDto dto, CancellationToken ct = default);
    Task<MenuItemDto> UpdateAsync(int id, MenuItemWriteDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
