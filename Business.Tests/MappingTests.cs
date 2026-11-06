using AutoMapperMappingException = AutoMapper.AutoMapperMappingException;
using Business.Models;
using Data.Entities;
using NUnit.Framework;
using static Business.Tests.Fixtures.BusinessTestData;

namespace Business.Tests;

public class MappingTests
{
    [Test]
    public void Profile_IsValid()
    {
        Configuration.AssertConfigurationIsValid();
    }

    [Test]
    public void ReadMappings_ProjectCompositionMenuAndSafeCustomerData()
    {
        var ingredient = new Ingredient { Id = 3, Name = "Salt" };
        var dish = new Dish { Id = 7, Name = "Salad", Description = "Fresh", IsAvailable = true };
        dish.DishIngredients.Add(new DishIngredient { Ingredient = ingredient, IngredientId = 3 });
        var dishDto = Mapper.Map<DishDto>(dish);
        Assert.That(dishDto.Ingredients.Single().Name, Is.EqualTo("Salt"));
        Assert.That(dishDto.Ingredients.Single().Id, Is.EqualTo(3));
        var menuDto = Mapper.Map<MenuItemDto>(new MenuItem
        {
            Id = 8, DishId = 7, Dish = dish, Price = 12.34m,
            PortionName = "Small", PortionDescription = "One bowl", IsAvailable = true
        });
        Assert.That(menuDto.DishName, Is.EqualTo("Salad"));
        Assert.That(menuDto.DishId, Is.EqualTo(7));
        Assert.That(menuDto.Price, Is.EqualTo(12.34m));
        var customer = new Customer
        {
            Id = 42, FirstName = "Private", LastName = "Person", PhoneNumber = "Secret",
            LoyaltyCode = new string('A', 32), LoyaltyLevel = new LoyaltyLevel { Name = "Basic" }
        };
        var registration = Mapper.Map<CustomerRegistrationResultDto>(customer);
        Assert.That(registration.LoyaltyCode, Is.EqualTo(customer.LoyaltyCode));
        Assert.That(registration.LevelName, Is.EqualTo("Basic"));
        Assert.That(typeof(CustomerRegistrationResultDto).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "LoyaltyCode", "LevelName", "DiscountPercent" }));
    }

    [TestCase(typeof(IngredientWriteDto), typeof(Ingredient))]
    [TestCase(typeof(DishWriteDto), typeof(Dish))]
    [TestCase(typeof(MenuItemWriteDto), typeof(MenuItem))]
    [TestCase(typeof(CustomerRegistrationDto), typeof(Customer))]
    public void WriteDtos_CannotAutomaticallyOverwriteEntities(Type dtoType, Type entityType)
    {
        var dto = Activator.CreateInstance(dtoType)!;
        var entity = (BaseEntity)Activator.CreateInstance(entityType)!;
        entity.Id = 42;

        Assert.Throws<AutoMapperMappingException>(() => Mapper.Map(dto, entity, dtoType, entityType));
        Assert.That(entity.Id, Is.EqualTo(42));
    }

    [Test]
    public void WriteContracts_ExposeOnlyAllowedFields()
    {
        Assert.That(typeof(IngredientWriteDto).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "Name" }));
        Assert.That(typeof(DishWriteDto).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "Name", "Description", "IsAvailable", "IngredientIds" }));
        Assert.That(typeof(MenuItemWriteDto).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "DishId", "PortionName", "PortionDescription", "Price", "IsAvailable" }));
        Assert.That(typeof(CustomerRegistrationDto).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "FirstName", "LastName", "PhoneNumber" }));
    }
}
