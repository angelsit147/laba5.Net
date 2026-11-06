using AutoMapper;
using Business.Interfaces;
using Business.Models;
using Business.Validation;
using Data.Entities;
using Data.Interfaces;

namespace Business.Services;

public class IngredientService : IIngredientService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IMapper mapper;

    public IngredientService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        this.unitOfWork = unitOfWork;
        this.mapper = mapper;
    }

    public async Task<IReadOnlyList<IngredientDto>> GetAllAsync(CancellationToken ct = default)
    {
        return mapper.Map<List<IngredientDto>>(await unitOfWork.Ingredients.GetAllAsync(ct));
    }

    public async Task<IngredientDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return mapper.Map<IngredientDto>(await FindAsync(id, ct));
    }

    public async Task<IngredientDto> CreateAsync(IngredientWriteDto dto, CancellationToken ct = default)
    {
        var entity = new Ingredient { Name = ValidateName(dto) };
        await unitOfWork.Ingredients.AddAsync(entity, ct);
        await unitOfWork.SaveAsync(ct);
        return mapper.Map<IngredientDto>(entity);
    }

    public async Task<IngredientDto> UpdateAsync(int id, IngredientWriteDto dto, CancellationToken ct = default)
    {
        var name = ValidateName(dto);
        var entity = await FindAsync(id, ct);
        entity.Name = name;
        unitOfWork.Ingredients.Update(entity);
        await unitOfWork.SaveAsync(ct);
        return mapper.Map<IngredientDto>(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await FindAsync(id, ct);
        if (await unitOfWork.Dishes.IsIngredientUsedAsync(id, ct))
        {
            throw new RestaurantException(RestaurantErrorType.Conflict, "ingredient_in_use",
                "The ingredient is used by a dish.");
        }

        unitOfWork.Ingredients.Remove(entity);
        await unitOfWork.SaveAsync(ct);
    }

    private async Task<Ingredient> FindAsync(int id, CancellationToken ct)
    {
        if (id <= 0)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_id", "Id must be positive.");
        }

        return await unitOfWork.Ingredients.GetByIdAsync(id, ct)
            ?? throw new RestaurantException(RestaurantErrorType.NotFound, "ingredient_not_found",
                "Ingredient was not found.");
    }

    private static string ValidateName(IngredientWriteDto? dto)
    {
        var name = dto?.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_name",
                "Ingredient name must contain 1 to 100 characters.");
        }

        return name;
    }
}
