using AutoMapper;
using Business.Interfaces;
using Business.Models;
using Business.Validation;
using Data.Entities;
using Data.Interfaces;

namespace Business.Services;

public class DishService : IDishService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IMapper mapper;

    public DishService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        this.unitOfWork = unitOfWork;
        this.mapper = mapper;
    }

    public async Task<IReadOnlyList<DishDto>> GetAllAsync(CancellationToken ct = default)
    {
        return mapper.Map<List<DishDto>>(await unitOfWork.Dishes.GetAllWithIngredientsAsync(ct));
    }

    public async Task<DishDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return mapper.Map<DishDto>(await FindAsync(id, ct));
    }

    public async Task<DishDto> CreateAsync(DishWriteDto dto, CancellationToken ct = default)
    {
        var (name, description) = Validate(dto);
        var ingredients = await FindIngredientsAsync(dto.IngredientIds, ct);
        var dish = new Dish { Name = name, Description = description, IsAvailable = dto.IsAvailable!.Value };
        foreach (var ingredient in ingredients)
        {
            dish.DishIngredients.Add(new DishIngredient
            {
                Dish = dish, IngredientId = ingredient.Id, Ingredient = ingredient
            });
        }

        await unitOfWork.Dishes.AddAsync(dish, ct);
        await unitOfWork.SaveAsync(ct);
        return mapper.Map<DishDto>(dish);
    }

    public async Task<DishDto> UpdateAsync(int id, DishWriteDto dto, CancellationToken ct = default)
    {
        var (name, description) = Validate(dto);
        var dish = await FindAsync(id, ct);

        var ingredients = await FindIngredientsAsync(dto.IngredientIds, ct);
        var ids = ingredients.Select(ingredient => ingredient.Id).ToHashSet();
        foreach (var link in dish.DishIngredients.Where(link => !ids.Contains(link.IngredientId)).ToList())
        {
            unitOfWork.Dishes.RemoveIngredientLink(link);
            dish.DishIngredients.Remove(link);
        }

        var retainedIds = dish.DishIngredients.Select(link => link.IngredientId).ToHashSet();
        foreach (var ingredient in ingredients.Where(ingredient => !retainedIds.Contains(ingredient.Id)))
        {
            dish.DishIngredients.Add(new DishIngredient
            {
                Dish = dish, DishId = dish.Id, IngredientId = ingredient.Id, Ingredient = ingredient
            });
        }

        dish.Name = name;
        dish.Description = description;
        dish.IsAvailable = dto.IsAvailable!.Value;
        unitOfWork.Dishes.Update(dish);
        await unitOfWork.SaveAsync(ct);
        return mapper.Map<DishDto>(dish);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var dish = await FindAsync(id, ct);
        if (await unitOfWork.Dishes.HasMenuItemsAsync(id, ct))
        {
            throw new RestaurantException(RestaurantErrorType.Conflict, "dish_in_menu",
                "A dish with menu items cannot be deleted.");
        }

        foreach (var link in dish.DishIngredients.ToList())
        {
            unitOfWork.Dishes.RemoveIngredientLink(link);
        }

        unitOfWork.Dishes.Remove(dish);
        await unitOfWork.SaveAsync(ct);
    }

    private async Task<Dish> FindAsync(int id, CancellationToken ct)
    {
        if (id <= 0)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_id", "Id must be positive.");
        }

        return await unitOfWork.Dishes.GetWithIngredientsAsync(id, ct)
            ?? throw new RestaurantException(RestaurantErrorType.NotFound, "dish_not_found", "Dish was not found.");
    }

    private async Task<List<Ingredient>> FindIngredientsAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var ingredients = new List<Ingredient>();
        foreach (var id in ids)
        {
            ingredients.Add(await unitOfWork.Ingredients.GetByIdAsync(id, ct)
                ?? throw new RestaurantException(RestaurantErrorType.NotFound, "ingredient_not_found",
                    "An ingredient of the dish was not found."));
        }

        return ingredients;
    }

    private static (string Name, string? Description) Validate(DishWriteDto? dto)
    {
        var name = dto?.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_name",
                "Dish name must contain 1 to 100 characters.");
        }

        var description = string.IsNullOrWhiteSpace(dto!.Description) ? null : dto.Description.Trim();
        if (description?.Length > 1000)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_description",
                "Dish description cannot exceed 1000 characters.");
        }

        if (dto.IsAvailable is null || dto.IngredientIds is null
            || dto.IngredientIds.Any(id => id <= 0)
            || dto.IngredientIds.Distinct().Count() != dto.IngredientIds.Count
            || (dto.IsAvailable.Value && dto.IngredientIds.Count == 0))
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_dish_ingredients",
                "Provide availability and unique positive ingredient Ids; an available dish needs ingredients.");
        }

        return (name, description);
    }
}
