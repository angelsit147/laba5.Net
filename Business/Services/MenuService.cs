using AutoMapper;
using Business.Interfaces;
using Business.Models;
using Business.Validation;
using Data.Entities;
using Data.Interfaces;

namespace Business.Services;

public class MenuService : IMenuService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IMapper mapper;

    public MenuService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        this.unitOfWork = unitOfWork;
        this.mapper = mapper;
    }

    public async Task<IReadOnlyList<MenuItemDto>> GetAllAsync(bool availableOnly = true, CancellationToken ct = default)
    {
        return mapper.Map<List<MenuItemDto>>(await unitOfWork.MenuItems.GetPriceListAsync(availableOnly, ct));
    }

    public async Task<MenuItemDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return mapper.Map<MenuItemDto>(await FindAsync(id, ct));
    }

    public async Task<MenuItemDto> CreateAsync(MenuItemWriteDto dto, CancellationToken ct = default)
    {
        var (portionName, description) = Validate(dto);
        var dish = await unitOfWork.Dishes.GetWithIngredientsAsync(dto.DishId, ct)
            ?? throw new RestaurantException(RestaurantErrorType.NotFound, "dish_not_found", "Dish was not found.");
        var item = new MenuItem
        {
            DishId = dish.Id, Dish = dish, PortionName = portionName, PortionDescription = description,
            Price = dto.Price, IsAvailable = dto.IsAvailable!.Value
        };
        await unitOfWork.MenuItems.AddAsync(item, ct);
        await unitOfWork.SaveAsync(ct);
        return mapper.Map<MenuItemDto>(item);
    }

    public async Task<MenuItemDto> UpdateAsync(int id, MenuItemWriteDto dto, CancellationToken ct = default)
    {
        var (portionName, description) = Validate(dto);
        var item = await FindAsync(id, ct);
        if (item.DishId != dto.DishId)
        {
            throw new RestaurantException(RestaurantErrorType.Conflict, "dish_id_immutable",
                "A menu item cannot be reassigned to another dish.");
        }

        item.PortionName = portionName;
        item.PortionDescription = description;
        item.Price = dto.Price;
        item.IsAvailable = dto.IsAvailable!.Value;
        unitOfWork.MenuItems.Update(item);
        await unitOfWork.SaveAsync(ct);
        return mapper.Map<MenuItemDto>(item);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var item = await FindAsync(id, ct);
        if (await unitOfWork.MenuItems.HasOrderItemsAsync(id, ct))
        {
            throw new RestaurantException(RestaurantErrorType.Conflict, "menu_item_in_use",
                "A menu item referenced by an order cannot be deleted.");
        }

        unitOfWork.MenuItems.Remove(item);
        await unitOfWork.SaveAsync(ct);
    }

    private async Task<MenuItem> FindAsync(int id, CancellationToken ct)
    {
        if (id <= 0)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_id", "Id must be positive.");
        }

        return await unitOfWork.MenuItems.GetWithDishAsync(id, ct)
            ?? throw new RestaurantException(RestaurantErrorType.NotFound, "menu_item_not_found",
                "Menu item was not found.");
    }

    private static (string PortionName, string Description) Validate(MenuItemWriteDto? dto)
    {
        if (dto is null || dto.DishId <= 0 || dto.IsAvailable is null)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_menu_item",
                "Provide a positive DishId and availability.");
        }

        var name = dto.PortionName?.Trim();
        var description = dto.PortionDescription?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100
            || string.IsNullOrEmpty(description) || description.Length > 500)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_portion",
                "Portion name must contain 1 to 100 and description 1 to 500 characters.");
        }

        if (dto.Price <= 0 || dto.Price > long.MaxValue / 100m
            || dto.Price * 100m != decimal.Truncate(dto.Price * 100m))
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_price",
                "Price must be positive, fit in Int64 cents and have at most two fractional digits.");
        }

        return (name, description);
    }
}
