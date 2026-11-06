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

public class IngredientServiceTests
{
    private Mock<IUnitOfWork> unitOfWork = null!;
    private Mock<IRepository<Ingredient>> ingredients = null!;
    private Mock<IDishRepository> dishes = null!;
    private IngredientService service = null!;
    private readonly CancellationToken ct = new CancellationTokenSource().Token;

    [SetUp]
    public void SetUp()
    {
        unitOfWork = CreateUnitOfWork();
        ingredients = Mock.Get(unitOfWork.Object.Ingredients);
        dishes = Mock.Get(unitOfWork.Object.Dishes);
        service = new IngredientService(unitOfWork.Object, Mapper);
    }

    [Test]
    public async Task Create_NormalizesNameAndSavesOnce()
    {
        Ingredient? added = null;
        ingredients.Setup(value => value.AddAsync(It.IsAny<Ingredient>(), ct))
            .Callback<Ingredient, CancellationToken>((entity, _) => { entity.Id = 9; added = entity; })
            .Returns(Task.CompletedTask);

        var result = await service.CreateAsync(new IngredientWriteDto { Name = "  Salt  " }, ct);

        Assert.That(added!.Name, Is.EqualTo("Salt"));
        Assert.That(result.Id, Is.EqualTo(9));
        Assert.That(result.Name, Is.EqualTo("Salt"));
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
        unitOfWork.Verify(value => value.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Read_ReturnsDtoAndDoesNotSave()
    {
        var entity = new Ingredient { Id = 7, Name = "Salt" };
        ingredients.Setup(value => value.GetByIdAsync(7, ct)).ReturnsAsync(entity);
        ingredients.Setup(value => value.GetAllAsync(ct)).ReturnsAsync(new[] { entity });

        Assert.That((await service.GetByIdAsync(7, ct)).Name, Is.EqualTo("Salt"));
        Assert.That((await service.GetAllAsync(ct)).Single().Id, Is.EqualTo(7));
        VerifyNoSave(unitOfWork);
    }

    [Test]
    public async Task Update_ChangesLoadedEntityAndSavesOnce()
    {
        var entity = new Ingredient { Id = 7, Name = "Salt" };
        ingredients.Setup(value => value.GetByIdAsync(7, ct)).ReturnsAsync(entity);

        Assert.That((await service.UpdateAsync(7, new IngredientWriteDto { Name = " Pepper " }, ct)).Name,
            Is.EqualTo("Pepper"));
        ingredients.Verify(value => value.Update(entity), Times.Once);
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Delete_OnlyUnusedIngredientCanBeRemoved(bool inUse)
    {
        var entity = new Ingredient { Id = 7, Name = "Salt" };
        ingredients.Setup(value => value.GetByIdAsync(7, ct)).ReturnsAsync(entity);
        dishes.Setup(value => value.IsIngredientUsedAsync(7, ct)).ReturnsAsync(inUse);
        if (inUse)
        {
            AssertError(async () => await service.DeleteAsync(7, ct), RestaurantErrorType.Conflict, "ingredient_in_use");
            ingredients.Verify(value => value.Remove(It.IsAny<Ingredient>()), Times.Never);
            VerifyNoSave(unitOfWork);
        }
        else
        {
            await service.DeleteAsync(7, ct);
            ingredients.Verify(value => value.Remove(entity), Times.Once);
            unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
        }
    }

    [TestCaseSource(typeof(BusinessTestData), nameof(InvalidNames))]
    public void InvalidName_DoesNotCreateOrModify(string? name)
    {
        var entity = new Ingredient { Id = 7, Name = "Original" };
        ingredients.Setup(value => value.GetByIdAsync(7, ct)).ReturnsAsync(entity);
        var dto = new IngredientWriteDto { Name = name! };
        AssertError(async () => await service.CreateAsync(dto, ct), RestaurantErrorType.Validation);
        AssertError(async () => await service.UpdateAsync(7, dto, ct), RestaurantErrorType.Validation);
        Assert.That(entity.Name, Is.EqualTo("Original"));
        ingredients.Verify(value => value.AddAsync(It.IsAny<Ingredient>(), It.IsAny<CancellationToken>()), Times.Never);
        ingredients.Verify(value => value.Update(It.IsAny<Ingredient>()), Times.Never);
        VerifyNoSave(unitOfWork);
    }

    [TestCase(0, RestaurantErrorType.Validation)]
    [TestCase(-1, RestaurantErrorType.Validation)]
    [TestCase(999, RestaurantErrorType.NotFound)]
    public void InvalidOrMissingId_ReturnsBusinessError(int id, RestaurantErrorType type)
    {
        AssertError(async () => await service.GetByIdAsync(id, ct), type);
        AssertError(async () => await service.UpdateAsync(id, new IngredientWriteDto { Name = "Valid" }, ct), type);
        AssertError(async () => await service.DeleteAsync(id, ct), type);
        VerifyNoSave(unitOfWork);
    }

    [Test]
    public void NullRequestAndCancellation_AreNotSilentlyAccepted()
    {
        AssertError(async () => await service.CreateAsync(null!, ct), RestaurantErrorType.Validation);
        ingredients.Setup(value => value.GetAllAsync(ct)).ThrowsAsync(new OperationCanceledException(ct));

        Assert.ThrowsAsync<OperationCanceledException>(async () => await service.GetAllAsync(ct));
        VerifyNoSave(unitOfWork);
    }

    [Test]
    public async Task MaximumNameLengthAndEmptyList_AreSupported()
    {
        ingredients.Setup(value => value.GetAllAsync(ct)).ReturnsAsync(Array.Empty<Ingredient>());
        Assert.That(await service.GetAllAsync(ct), Is.Empty);
        Assert.That((await service.CreateAsync(new IngredientWriteDto { Name = new string('a', 100) }, ct)).Name.Length,
            Is.EqualTo(100));
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
    }
}
