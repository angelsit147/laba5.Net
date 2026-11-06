using AutoMapper;
using Business.Models;
using Data.Entities;

namespace Business;

public class AutomapperProfile : Profile
{
    public AutomapperProfile()
    {
        CreateMap<Ingredient, IngredientDto>();
        CreateMap<LoyaltyLevel, LoyaltyLevelDto>();
        CreateMap<Dish, DishDto>()
            .ForMember(dto => dto.Ingredients,
                options => options.MapFrom(entity => entity.DishIngredients.Select(link => link.Ingredient)));
        CreateMap<MenuItem, MenuItemDto>()
            .ForMember(dto => dto.DishName, options => options.MapFrom(entity => entity.Dish.Name));
        CreateMap<Customer, LoyaltyCardDto>()
            .ForMember(dto => dto.LevelName, options => options.MapFrom(entity => entity.LoyaltyLevel.Name))
            .ForMember(dto => dto.DiscountPercent,
                options => options.MapFrom(entity => entity.LoyaltyLevel.DiscountPercent));
        CreateMap<Customer, CustomerRegistrationResultDto>()
            .ForMember(dto => dto.LevelName, options => options.MapFrom(entity => entity.LoyaltyLevel.Name))
            .ForMember(dto => dto.DiscountPercent,
                options => options.MapFrom(entity => entity.LoyaltyLevel.DiscountPercent));
    }
}
