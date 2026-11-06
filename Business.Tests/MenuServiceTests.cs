using System.Globalization;
using Business.Models;
using Business.Services;
using Business.Validation;
using Data.Entities;
using Data.Interfaces;
using Moq;
using NUnit.Framework;
using static Business.Tests.Fixtures.BusinessTestData;

namespace Business.Tests;

public class MenuServiceTests
{
    private Mock<IUnitOfWork> unitOfWork = null!;
    private Mock<IMenuItemRepository> menu = null!;
    private Mock<IDishRepository> dishes = null!;
    private MenuService service = null!;
    private readonly CancellationToken ct = new CancellationTokenSource().Token;

    [SetUp]
    public void SetUp()
    {
        unitOfWork = CreateUnitOfWork();
        menu = Mock.Get(unitOfWork.Object.MenuItems);
        dishes = Mock.Get(unitOfWork.Object.Dishes);
        dishes.Setup(value => value.GetWithIngredientsAsync(7, ct)).ReturnsAsync(new Dish { Id = 7, Name = "Salad" });
        service = new MenuService(unitOfWork.Object, Mapper);
    }

    private static MenuItemWriteDto Write(decimal price = 123.45m) =>
        new() { DishId = 7, PortionName = " Regular ", PortionDescription = " One bowl ", Price = price, IsAvailable = true };

    private static MenuItem Item() => new()
    {
        Id = 8, DishId = 7, Dish = new Dish { Id = 7, Name = "Salad" },
        PortionName = "Original", PortionDescription = "Original description", Price = 10m
    };

    [Test]
    public async Task Create_UsesExistingDishAndSavesOnce()
    {
        MenuItem? added = null;
        menu.Setup(value => value.AddAsync(It.IsAny<MenuItem>(), ct))
            .Callback<MenuItem, CancellationToken>((entity, _) => { entity.Id = 8; added = entity; })
            .Returns(Task.CompletedTask);

        var result = await service.CreateAsync(Write(), ct);

        Assert.That(added!.DishId, Is.EqualTo(7));
        Assert.That(result.DishName, Is.EqualTo("Salad"));
        Assert.That(result.PortionName, Is.EqualTo("Regular"));
        Assert.That(result.PortionDescription, Is.EqualTo("One bowl"));
        Assert.That(result.Price, Is.EqualTo(123.45m));
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
    }

    [Test]
    public async Task Update_ChangesAllowedFieldsAndSavesOnce()
    {
        var item = Item();
        menu.Setup(value => value.GetWithDishAsync(8, ct)).ReturnsAsync(item);
        var dto = Write();
        dto.IsAvailable = false;

        var result = await service.UpdateAsync(8, dto, ct);

        Assert.That(result.Price, Is.EqualTo(123.45m));
        Assert.That(result.IsAvailable, Is.False);
        Assert.That(item.DishId, Is.EqualTo(7));
        menu.Verify(value => value.Update(item), Times.Once);
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("0.001")]
    [TestCase("92233720368547758.08")]
    [TestCase("79228162514264337593543950335")]
    public void InvalidPrice_DoesNotCreateOrMutate(string text)
    {
        var item = Item();
        menu.Setup(value => value.GetWithDishAsync(8, ct)).ReturnsAsync(item);
        var dto = Write(decimal.Parse(text, CultureInfo.InvariantCulture));

        AssertError(async () => await service.CreateAsync(dto, ct), RestaurantErrorType.Validation, "invalid_price");
        AssertError(async () => await service.UpdateAsync(8, dto, ct), RestaurantErrorType.Validation, "invalid_price");
        Assert.That(item.Price, Is.EqualTo(10m));
        menu.Verify(value => value.Update(It.IsAny<MenuItem>()), Times.Never);
        menu.Verify(value => value.AddAsync(It.IsAny<MenuItem>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoSave(unitOfWork);
    }

    [TestCase("0.01")]
    [TestCase("12.34")]
    [TestCase("92233720368547758.07")]
    public async Task ValidMoneyBoundaries_AreAccepted(string text)
    {
        var value = decimal.Parse(text, CultureInfo.InvariantCulture);
        Assert.That((await service.CreateAsync(Write(value), ct)).Price, Is.EqualTo(value));
        unitOfWork.Verify(entity => entity.SaveAsync(ct), Times.Once);
    }

    [TestCase("empty_name")]
    [TestCase("long_name")]
    [TestCase("null_name")]
    [TestCase("empty_description")]
    [TestCase("long_description")]
    [TestCase("null_description")]
    [TestCase("missing_flag")]
    [TestCase("invalid_dish_id")]
    public void InvalidPortion_IsRejected(string scenario)
    {
        var dto = Write();
        switch (scenario)
        {
            case "empty_name": dto.PortionName = "  "; break;
            case "long_name": dto.PortionName = new string('n', 101); break;
            case "null_name": dto.PortionName = null!; break;
            case "empty_description": dto.PortionDescription = " "; break;
            case "long_description": dto.PortionDescription = new string('d', 501); break;
            case "null_description": dto.PortionDescription = null!; break;
            case "missing_flag": dto.IsAvailable = null; break;
            case "invalid_dish_id": dto.DishId = 0; break;
        }

        AssertError(async () => await service.CreateAsync(dto, ct), RestaurantErrorType.Validation);
        AssertError(async () => await service.CreateAsync(null!, ct), RestaurantErrorType.Validation);
        VerifyNoSave(unitOfWork);
    }

    [Test]
    public async Task MaximumPortionLengths_AreAccepted()
    {
        var dto = Write();
        dto.PortionName = new string('n', 100);
        dto.PortionDescription = new string('d', 500);
        var result = await service.CreateAsync(dto, ct);
        Assert.That(result.PortionName.Length, Is.EqualTo(100));
        Assert.That(result.PortionDescription.Length, Is.EqualTo(500));
    }

    [Test]
    public void UnknownDishAndReassignment_AreRejectedWithoutChanges()
    {
        var dto = Write();
        dto.DishId = 99;
        AssertError(async () => await service.CreateAsync(dto, ct), RestaurantErrorType.NotFound, "dish_not_found");
        var item = Item();
        menu.Setup(value => value.GetWithDishAsync(8, ct)).ReturnsAsync(item);

        AssertError(async () => await service.UpdateAsync(8, dto, ct), RestaurantErrorType.Conflict, "dish_id_immutable");
        Assert.That(item.DishId, Is.EqualTo(7));
        Assert.That(item.PortionName, Is.EqualTo("Original"));
        menu.Verify(value => value.Update(It.IsAny<MenuItem>()), Times.Never);
        VerifyNoSave(unitOfWork);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task List_ForwardsAvailabilityFilterToTestedDataQuery(bool availableOnly)
    {

        var expected = availableOnly ? Array.Empty<MenuItem>() : new[] { Item() };
        menu.Setup(value => value.GetPriceListAsync(availableOnly, ct)).ReturnsAsync(expected);

        Assert.That(await service.GetAllAsync(availableOnly, ct), Has.Count.EqualTo(expected.Length));
        menu.Verify(value => value.GetPriceListAsync(availableOnly, ct), Times.Once);
        VerifyNoSave(unitOfWork);
    }

    [Test]
    public async Task ReadAndDelete_ProtectHistoricalReferences()
    {
        var item = Item();
        menu.Setup(value => value.GetWithDishAsync(8, ct)).ReturnsAsync(item);
        Assert.That((await service.GetByIdAsync(8, ct)).DishName, Is.EqualTo("Salad"));
        menu.Setup(value => value.HasOrderItemsAsync(8, ct)).ReturnsAsync(true);
        AssertError(async () => await service.DeleteAsync(8, ct), RestaurantErrorType.Conflict, "menu_item_in_use");
        menu.Verify(value => value.Remove(It.IsAny<MenuItem>()), Times.Never);
        VerifyNoSave(unitOfWork);

        menu.Setup(value => value.HasOrderItemsAsync(8, ct)).ReturnsAsync(false);
        await service.DeleteAsync(8, ct);
        menu.Verify(value => value.Remove(item), Times.Once);
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
    }

    [TestCase(0, RestaurantErrorType.Validation)]
    [TestCase(999, RestaurantErrorType.NotFound)]
    public void InvalidOrMissingId_RejectsOperations(int id, RestaurantErrorType type)
    {
        AssertError(async () => await service.GetByIdAsync(id, ct), type);
        AssertError(async () => await service.UpdateAsync(id, Write(), ct), type);
        AssertError(async () => await service.DeleteAsync(id, ct), type);
        VerifyNoSave(unitOfWork);
    }
}
