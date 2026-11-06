using Business.Interfaces;
using Business.Models;
using Business.Services;
using Business.Validation;
using Data.Entities;
using Data.Enums;
using Data.Interfaces;
using Moq;
using NUnit.Framework;
using static Business.Tests.Fixtures.BusinessTestData;

namespace Business.Tests;

public class OrderServiceTests
{
    private Mock<IUnitOfWork> work = null!;
    private Mock<IOrderRepository> orders = null!;
    private OrderService service = null!;
    private readonly CancellationToken ct = new CancellationTokenSource().Token;

    [SetUp]
    public void SetUp()
    {
        work = CreateUnitOfWork();
        orders = Mock.Get(work.Object.Orders);
        service = new OrderService(work.Object, Mock.Of<ILoyaltyService>());
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Create_ProducesEmptyUtcDraftWithGuestOrResolvedCard(bool customer)
    {
        var known = TestOrder().Customer!;
        var code = new string('A', 32);
        Mock.Get(work.Object.Customers).Setup(value => value.FindByLoyaltyCodeAsync(code, ct)).ReturnsAsync(known);
        Order? saved = null;
        orders.Setup(value => value.AddAsync(It.IsAny<Order>(), ct))
            .Callback<Order, CancellationToken>((order, _) => { saved = order; order.Id = 9; })
            .Returns(Task.CompletedTask);
        var result = await service.CreateAsync(new CreateOrderDto
        {
            OrderType = OrderType.Delivery, LoyaltyCode = customer ? " " + code.ToLowerInvariant() + " " : null
        }, ct);

        Assert.That(result.Id, Is.EqualTo(9));
        Assert.That(result.Status, Is.EqualTo(OrderStatus.Draft));
        Assert.That(result.CreatedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(result.OrderType, Is.EqualTo(OrderType.Delivery));
        Assert.That(result.Items, Is.Empty);
        Assert.That(result.Total, Is.Zero);
        Assert.That(result.DiscountPercentFixed, Is.Null);
        Assert.That(saved!.CustomerId, Is.EqualTo(customer ? known.Id : (int?)null));
        Assert.That(result.AppliedDiscountPercent, Is.EqualTo(customer ? 5m : 0m));
        work.Verify(value => value.SaveAsync(ct), Times.Once);
        work.Verify(value => value.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("")]
    [TestCase("  ")]
    [TestCase("bad")]
    [TestCase("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public void InvalidCode_DoesNotCreateOrder(string code)
    {
        AssertError(async () => await service.CreateAsync(new CreateOrderDto { LoyaltyCode = code }, ct),
            RestaurantErrorType.Validation);
        orders.Verify(value => value.AddAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoSave(work);
    }

    [Test]
    public void UnknownCodeAndInvalidOrderType_AreRejected()
    {
        AssertError(async () => await service.CreateAsync(new CreateOrderDto { LoyaltyCode = new string('F', 32) }, ct),
            RestaurantErrorType.NotFound);
        AssertError(async () => await service.CreateAsync(new CreateOrderDto { OrderType = (OrderType)99 }, ct),
            RestaurantErrorType.Validation);
        AssertError(async () => await service.CreateAsync(null!, ct), RestaurantErrorType.Validation);
        VerifyNoSave(work);
    }

    [Test]
    public async Task DraftUpdate_CanRemoveOrReplaceCard()
    {
        var order = TestOrder();
        orders.Setup(value => value.GetWithCurrentMenuAsync(7, true, ct)).ReturnsAsync(order);
        var guest = await service.UpdateDraftAsync(7, new UpdateDraftOrderDto { OrderType = OrderType.Takeaway }, ct);
        Assert.That(order.CustomerId, Is.Null);
        Assert.That(guest.AppliedDiscountPercent, Is.Zero);
        var customer = TestOrder().Customer!;
        Mock.Get(work.Object.Customers).Setup(value => value.FindByLoyaltyCodeAsync(new string('A', 32), ct)).ReturnsAsync(customer);
        await service.UpdateDraftAsync(7, new UpdateDraftOrderDto { LoyaltyCode = new string('a', 32) }, ct);
        Assert.That(order.Customer, Is.SameAs(customer));
        work.Verify(value => value.SaveAsync(ct), Times.Exactly(2));
    }

    [Test]
    public async Task Add_MergesExistingPortionAndAddsDifferentPortionWithSnapshots()
    {
        var order = TestOrder();
        orders.Setup(value => value.GetWithCurrentMenuAsync(7, true, ct)).ReturnsAsync(order);
        var menu = TestMenu(3, 125m);
        Mock.Get(work.Object.MenuItems).Setup(value => value.GetWithDishAsync(3, ct)).ReturnsAsync(menu);
        Mock.Get(work.Object.MenuItems).Setup(value => value.GetWithDishAsync(4, ct)).ReturnsAsync(TestMenu(4, 50m));
        await service.AddItemAsync(7, new AddOrderItemDto { MenuItemId = 3, Quantity = 3 }, ct);
        var result = await service.AddItemAsync(7, new AddOrderItemDto { MenuItemId = 4, Quantity = 1 }, ct);

        Assert.That(result.Items, Has.Count.EqualTo(2));
        var existing = order.Items.Single(item => item.MenuItemId == 3);
        Assert.That(existing.Quantity, Is.EqualTo(5));
        Assert.That(existing.UnitPrice, Is.EqualTo(125m));
        Assert.That(existing.DishNameFixed, Is.EqualTo("Current dish"));
        Assert.That(order.Items.Single(item => item.MenuItemId == 4).PortionDescriptionFixed, Is.EqualTo("Current description"));
        work.Verify(value => value.SaveAsync(ct), Times.Exactly(2));
    }

    [Test]
    public async Task UpdateSetsExactQuantityAndRemoveDeletesOnlyOwnRow()
    {
        var order = TestOrder();
        var item = order.Items.Single();
        orders.Setup(value => value.GetWithCurrentMenuAsync(7, true, ct)).ReturnsAsync(order);
        Assert.That((await service.UpdateItemAsync(7, 71, new UpdateOrderItemDto { Quantity = 1001 }, ct)).Items.Single().Quantity,
            Is.EqualTo(1001));
        await service.RemoveItemAsync(7, 71, ct);
        Assert.That(order.Items, Is.Empty);
        orders.Verify(value => value.RemoveOrderItem(item), Times.Once);
        work.Verify(value => value.SaveAsync(ct), Times.Exactly(2));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void InvalidQuantity_DoesNotSave(int quantity)
    {
        AssertError(async () => await service.AddItemAsync(7, new AddOrderItemDto { MenuItemId = 3, Quantity = quantity }, ct),
            RestaurantErrorType.Validation);
        AssertError(async () => await service.UpdateItemAsync(7, 71, new UpdateOrderItemDto { Quantity = quantity }, ct),
            RestaurantErrorType.Validation);
        VerifyNoSave(work);
    }

    [Test]
    public void MissingOrderPortionAndForeignRow_AreNotFound()
    {
        AssertError(async () => await service.GetByIdAsync(999, ct), RestaurantErrorType.NotFound);
        AssertError(async () => await service.GetByIdAsync(0, ct), RestaurantErrorType.Validation);
        var order = TestOrder();
        orders.Setup(value => value.GetWithCurrentMenuAsync(7, true, ct)).ReturnsAsync(order);
        AssertError(async () => await service.AddItemAsync(7, new AddOrderItemDto { MenuItemId = 999, Quantity = 1 }, ct),
            RestaurantErrorType.NotFound);
        AssertError(async () => await service.UpdateItemAsync(7, 99, new UpdateOrderItemDto { Quantity = 1 }, ct),
            RestaurantErrorType.NotFound);
        AssertError(async () => await service.RemoveItemAsync(7, 99, ct), RestaurantErrorType.NotFound);
        Assert.That(order.Items.Single().Quantity, Is.EqualTo(2));
        VerifyNoSave(work);
    }

    [TestCase("portion")]
    [TestCase("dish")]
    [TestCase("ingredients")]
    public void Add_RequiresFullAvailability(string reason)
    {
        var order = TestOrder();
        var menu = TestMenu();
        if (reason == "portion") menu.IsAvailable = false;
        if (reason == "dish") menu.Dish.IsAvailable = false;
        if (reason == "ingredients") menu.Dish.DishIngredients.Clear();
        orders.Setup(value => value.GetWithCurrentMenuAsync(7, true, ct)).ReturnsAsync(order);
        Mock.Get(work.Object.MenuItems).Setup(value => value.GetWithDishAsync(3, ct)).ReturnsAsync(menu);

        AssertError(async () => await service.AddItemAsync(7, new AddOrderItemDto { MenuItemId = 3, Quantity = 1 }, ct),
            RestaurantErrorType.Conflict);
        Assert.That(order.Items.Single().Quantity, Is.EqualTo(2));
        VerifyNoSave(work);
    }

    [Test]
    public void MergedQuantityOverflow_DoesNotMutateRow()
    {
        var order = TestOrder();
        order.Items.Single().Quantity = int.MaxValue;
        orders.Setup(value => value.GetWithCurrentMenuAsync(7, true, ct)).ReturnsAsync(order);
        Mock.Get(work.Object.MenuItems).Setup(value => value.GetWithDishAsync(3, ct)).ReturnsAsync(TestMenu());
        AssertError(async () => await service.AddItemAsync(7, new AddOrderItemDto { MenuItemId = 3, Quantity = 1 }, ct),
            RestaurantErrorType.Validation, "quantity_overflow");
        Assert.That(order.Items.Single().Quantity, Is.EqualTo(int.MaxValue));
        VerifyNoSave(work);
    }
}
