using Business.Models;
using Business.Services;
using Business.Tests.Fixtures;
using Business.Validation;
using Data.Entities;
using Data.Interfaces;
using Moq;
using NUnit.Framework;
using static Business.Tests.Fixtures.BusinessTestData;

namespace Business.Tests;

public class DishServiceTests
{
    private Mock<IUnitOfWork> unitOfWork = null!;
    private Mock<IDishRepository> dishes = null!;
    private Mock<IRepository<Ingredient>> ingredients = null!;
    private DishService service = null!;
    private readonly CancellationToken ct = new CancellationTokenSource().Token;

    [SetUp]
    public void SetUp()
    {
        unitOfWork = CreateUnitOfWork();
        dishes = Mock.Get(unitOfWork.Object.Dishes);
        ingredients = Mock.Get(unitOfWork.Object.Ingredients);
        ingredients.Setup(value => value.GetByIdAsync(1, ct)).ReturnsAsync(new Ingredient { Id = 1, Name = "Salt" });
        ingredients.Setup(value => value.GetByIdAsync(2, ct)).ReturnsAsync(new Ingredient { Id = 2, Name = "Tomato" });
        ingredients.Setup(value => value.GetByIdAsync(3, ct)).ReturnsAsync(new Ingredient { Id = 3, Name = "Oil" });
        service = new DishService(unitOfWork.Object, Mapper);
    }

    private static Dish ExistingDish()
    {
        var dish = new Dish { Id = 7, Name = "Original", Description = "Original description", IsAvailable = true };
        dish.DishIngredients.Add(new DishIngredient
        {
            Id = 71, DishId = 7, Dish = dish, IngredientId = 1, Ingredient = new Ingredient { Id = 1, Name = "Salt" }
        });
        dish.DishIngredients.Add(new DishIngredient
        {
            Id = 72, DishId = 7, Dish = dish, IngredientId = 2, Ingredient = new Ingredient { Id = 2, Name = "Tomato" }
        });
        return dish;
    }

    private static DishWriteDto Write(params int[] ids) =>
        new() { Name = " Salad ", Description = " Fresh ", IsAvailable = true, IngredientIds = ids };

    [Test]
    public async Task Create_UsesIngredientsAndSavesOneAggregate()
    {
        Dish? added = null;
        dishes.Setup(value => value.AddAsync(It.IsAny<Dish>(), ct))
            .Callback<Dish, CancellationToken>((entity, _) => { entity.Id = 8; added = entity; })
            .Returns(Task.CompletedTask);

        var result = await service.CreateAsync(Write(1, 2), ct);

        Assert.That(result.Id, Is.EqualTo(8));
        Assert.That(result.Name, Is.EqualTo("Salad"));
        Assert.That(result.Description, Is.EqualTo("Fresh"));
        Assert.That(result.Ingredients.Select(value => value.Name), Is.EquivalentTo(new[] { "Salt", "Tomato" }));
        Assert.That(added!.DishIngredients.All(link => ReferenceEquals(link.Dish, added)), Is.True);
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
    }

    [Test]
    public async Task EmptyUnavailableDish_CanBeCreatedAndExistingDishDeactivated()
    {
        var dto = Write();
        dto.IsAvailable = false;
        dto.Description = "  ";
        Assert.That((await service.CreateAsync(dto, ct)).Ingredients, Is.Empty);
        var dish = ExistingDish();
        dishes.Setup(value => value.GetWithIngredientsAsync(7, ct)).ReturnsAsync(dish);

        var result = await service.UpdateAsync(7, dto, ct);

        Assert.That(result.Ingredients, Is.Empty);
        Assert.That(result.IsAvailable, Is.False);
        Assert.That(result.Description, Is.Null);
        dishes.Verify(value => value.RemoveIngredientLink(It.IsAny<DishIngredient>()), Times.Exactly(2));
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Exactly(2));
    }

    [Test]
    public async Task Update_ReplacesFullSetPreservingRetainedLink()
    {
        var dish = ExistingDish();
        var retained = dish.DishIngredients.Last();
        var removed = dish.DishIngredients.First();
        dishes.Setup(value => value.GetWithIngredientsAsync(7, ct)).ReturnsAsync(dish);

        var result = await service.UpdateAsync(7, Write(2, 3), ct);

        Assert.That(result.Ingredients.Select(value => value.Id), Is.EquivalentTo(new[] { 2, 3 }));
        Assert.That(dish.DishIngredients.Single(link => link.IngredientId == 2), Is.SameAs(retained));
        Assert.That(dish.DishIngredients.Count, Is.EqualTo(2));
        dishes.Verify(value => value.RemoveIngredientLink(removed), Times.Once);
        dishes.Verify(value => value.Update(dish), Times.Once);
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
    }

    [Test]
    public void UnknownIngredient_DoesNotMutateTrackedDishOrPartiallySave()
    {
        var dish = ExistingDish();
        var original = dish.DishIngredients.ToArray();
        dishes.Setup(value => value.GetWithIngredientsAsync(7, ct)).ReturnsAsync(dish);

        AssertError(async () => await service.UpdateAsync(7, Write(1, 999), ct),
            RestaurantErrorType.NotFound, "ingredient_not_found");
        Assert.That(dish.Name, Is.EqualTo("Original"));
        Assert.That(dish.Description, Is.EqualTo("Original description"));
        Assert.That(dish.IsAvailable, Is.True);
        Assert.That(dish.DishIngredients, Is.EqualTo(original));
        dishes.Verify(value => value.Update(It.IsAny<Dish>()), Times.Never);
        dishes.Verify(value => value.RemoveIngredientLink(It.IsAny<DishIngredient>()), Times.Never);
        VerifyNoSave(unitOfWork);
    }

    [TestCase("empty")]
    [TestCase("duplicate")]
    [TestCase("nonpositive")]
    [TestCase("missing_flag")]
    [TestCase("null_ids")]
    [TestCase("long_description")]
    public void InvalidDish_DoesNotChangeOrSave(string scenario)
    {
        var dish = ExistingDish();
        dishes.Setup(value => value.GetWithIngredientsAsync(7, ct)).ReturnsAsync(dish);
        var dto = Write(1);
        switch (scenario)
        {
            case "empty": dto.IngredientIds = Array.Empty<int>(); break;
            case "duplicate": dto.IngredientIds = new[] { 1, 1 }; break;
            case "nonpositive": dto.IngredientIds = new[] { 0 }; break;
            case "missing_flag": dto.IsAvailable = null; break;
            case "null_ids": dto.IngredientIds = null!; break;
            case "long_description": dto.Description = new string('d', 1001); break;
        }

        AssertError(async () => await service.CreateAsync(dto, ct), RestaurantErrorType.Validation);
        AssertError(async () => await service.UpdateAsync(7, dto, ct), RestaurantErrorType.Validation);
        Assert.That(dish.Name, Is.EqualTo("Original"));
        Assert.That(dish.DishIngredients, Has.Count.EqualTo(2));
        VerifyNoSave(unitOfWork);
    }

    [TestCaseSource(typeof(BusinessTestData), nameof(InvalidNames))]
    public void InvalidNameAndNullRequest_AreRejected(string? name)
    {
        var dto = Write(1);
        dto.Name = name!;
        AssertError(async () => await service.CreateAsync(dto, ct), RestaurantErrorType.Validation);
        AssertError(async () => await service.CreateAsync(null!, ct), RestaurantErrorType.Validation);
        VerifyNoSave(unitOfWork);
    }

    [Test]
    public async Task Reads_ReturnCompositionWithoutSave()
    {
        var dish = ExistingDish();
        dishes.Setup(value => value.GetWithIngredientsAsync(7, ct)).ReturnsAsync(dish);
        dishes.Setup(value => value.GetAllWithIngredientsAsync(ct)).ReturnsAsync(new[] { dish });

        Assert.That((await service.GetByIdAsync(7, ct)).Ingredients, Has.Count.EqualTo(2));
        Assert.That((await service.GetAllAsync(ct)).Single().Id, Is.EqualTo(7));
        VerifyNoSave(unitOfWork);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Delete_ProtectsMenuReferencesAndExplicitlyRemovesLinks(bool hasMenu)
    {
        var dish = ExistingDish();
        dishes.Setup(value => value.GetWithIngredientsAsync(7, ct)).ReturnsAsync(dish);
        dishes.Setup(value => value.HasMenuItemsAsync(7, ct)).ReturnsAsync(hasMenu);
        if (hasMenu)
        {
            AssertError(async () => await service.DeleteAsync(7, ct), RestaurantErrorType.Conflict, "dish_in_menu");
            dishes.Verify(value => value.RemoveIngredientLink(It.IsAny<DishIngredient>()), Times.Never);
            dishes.Verify(value => value.Remove(It.IsAny<Dish>()), Times.Never);
            VerifyNoSave(unitOfWork);
        }
        else
        {
            await service.DeleteAsync(7, ct);
            dishes.Verify(value => value.RemoveIngredientLink(It.IsAny<DishIngredient>()), Times.Exactly(2));
            dishes.Verify(value => value.Remove(dish), Times.Once);
            unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
        }
    }

    [TestCase(0, RestaurantErrorType.Validation)]
    [TestCase(999, RestaurantErrorType.NotFound)]
    public void MissingDish_RejectsReadUpdateDelete(int id, RestaurantErrorType type)
    {
        AssertError(async () => await service.GetByIdAsync(id, ct), type);
        AssertError(async () => await service.UpdateAsync(id, Write(1), ct), type);
        AssertError(async () => await service.DeleteAsync(id, ct), type);
        VerifyNoSave(unitOfWork);
    }

    [Test]
    public async Task ValidMaximumLengths_AreAccepted()
    {
        var dto = Write(1);
        dto.Name = new string('n', 100);
        dto.Description = new string('d', 1000);

        var result = await service.CreateAsync(dto, ct);

        Assert.That(result.Name.Length, Is.EqualTo(100));
        Assert.That(result.Description!.Length, Is.EqualTo(1000));
    }
}
