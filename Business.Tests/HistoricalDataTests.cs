using Business.Interfaces;
using Business.Models;
using Business.Services;
using Business.Validation;
using Data.Data;
using Data.Entities;
using Data.Enums;
using Data.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using static Business.Tests.Fixtures.BusinessTestData;

namespace Business.Tests;

public class HistoricalDataTests
{
    [TestCase(OrderStatus.Draft, "customer_level_missing")]
    [TestCase(OrderStatus.Confirmed, "missing_fixed_discount")]
    [TestCase(OrderStatus.Completed, "missing_fixed_discount")]
    public void MissingDiscountSource_IsRejectedWithoutWriting(OrderStatus status, string code)
    {
        var work = CreateUnitOfWork();
        var order = TestOrder(status);
        order.Customer!.LoyaltyLevel = null!;
        order.DiscountPercentFixed = null;
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        AssertError(async () => await new OrderService(work.Object, Mock.Of<ILoyaltyService>()).GetByIdAsync(7),
            RestaurantErrorType.Conflict, code);
        Assert.That(order.Status, Is.EqualTo(status));
        VerifyNoSave(work);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task DraftRead_UsesCurrentPricesDiscountAndAvailabilityWithoutWriting(bool available)
    {
        var work = CreateUnitOfWork();
        var order = TestOrder();
        var item = order.Items.Single();
        item.MenuItem.Price = 120m;
        item.MenuItem.IsAvailable = available;
        order.Customer!.LoyaltyLevel.DiscountPercent = 10m;
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var result = await new OrderService(work.Object, Mock.Of<ILoyaltyService>()).GetByIdAsync(7);
        Assert.That(result.Subtotal, Is.EqualTo(240m));
        Assert.That(result.Total, Is.EqualTo(216m));
        Assert.That(result.Items.Single().IsAvailable, Is.EqualTo(available));
        Assert.That(result.Items.Single().DishName, Is.EqualTo("Current dish"));
        Assert.That(result.IsEstimate, Is.True);
        Assert.That(result.DiscountPercentFixed, Is.Null);
        Assert.That(item.UnitPrice, Is.EqualTo(80m));
        Assert.That(item.DishNameFixed, Is.EqualTo("Old dish"));
        VerifyNoSave(work);
    }

    [TestCase(OrderStatus.Confirmed)]
    [TestCase(OrderStatus.Completed)]
    public async Task HistoricalRead_IgnoresCurrentMenuAndCustomerLevel(OrderStatus status)
    {
        var work = CreateUnitOfWork();
        var order = TestOrder(status);
        order.Items.Single().MenuItem = null!;
        order.Customer!.LoyaltyLevel.DiscountPercent = 100m;
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var result = await new OrderService(work.Object, Mock.Of<ILoyaltyService>()).GetByIdAsync(7);
        Assert.That(result.Total, Is.EqualTo(152m));
        Assert.That(result.Items.Single().DishName, Is.EqualTo("Old dish"));
        Assert.That(result.Items.Single().IsAvailable, Is.Null);
        Assert.That(result.AppliedDiscountPercent, Is.EqualTo(5m));
        Assert.That(result.IsEstimate, Is.False);
        VerifyNoSave(work);
    }

    [TestCase(OrderStatus.Draft, true, "160")]
    [TestCase(OrderStatus.Confirmed, false, "152")]
    public async Task Cancel_PreservesPreviousSnapshotsAndDiscount(OrderStatus initial, bool estimate, string total)
    {
        var work = CreateUnitOfWork();
        var order = TestOrder(initial);
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var result = await new OrderService(work.Object, Mock.Of<ILoyaltyService>()).CancelAsync(7);
        Assert.That(result.IsEstimate, Is.EqualTo(estimate));
        Assert.That(result.Total, Is.EqualTo(decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture)));
        Assert.That(order.Items.Single().UnitPrice, Is.EqualTo(80m));
        Assert.That(order.Items.Single().DishNameFixed, Is.EqualTo("Old dish"));
        work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Confirm_RefreshesAllSnapshotsInsideOneTransactionAndFreezesThem()
    {
        var work = CreateUnitOfWork();
        var order = TestOrder();
        order.Items.Single().MenuItem.Price = 120m;
        order.Customer!.LoyaltyLevel.DiscountPercent = 10m;
        var inTransaction = false;
        work.Setup(value => value.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(async (Func<CancellationToken, Task> operation, CancellationToken ct) =>
            {
                inTransaction = true;
                try { await operation(ct); }
                finally { inTransaction = false; }
            });
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, true, It.IsAny<CancellationToken>()))
            .Callback(() => Assert.That(inTransaction, Is.True)).ReturnsAsync(order);
        work.Setup(value => value.SaveAsync(It.IsAny<CancellationToken>()))
            .Callback(() => Assert.That(inTransaction, Is.True)).ReturnsAsync(1);
        var service = new OrderService(work.Object, Mock.Of<ILoyaltyService>());
        var result = await service.ConfirmAsync(7);
        Assert.That(result.Total, Is.EqualTo(216m));
        Assert.That(order.Items.Single().UnitPrice, Is.EqualTo(120m));
        Assert.That(order.Items.Single().PortionNameFixed, Is.EqualTo("Current portion"));
        Assert.That(order.Items.Single().PortionDescriptionFixed, Is.EqualTo("Current description"));
        order.Items.Single().MenuItem.Price = 999m;
        order.Customer.LoyaltyLevel.DiscountPercent = 100m;
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        Assert.That((await service.GetByIdAsync(7)).Total, Is.EqualTo(216m));
        work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("unavailable")]
    [TestCase("invalid_price")]
    public void InvalidSecondRow_DoesNotPartiallyConfirmFirstRow(string reason)
    {
        var work = CreateUnitOfWork();
        var order = TestOrder();
        var menu = TestMenu(4);
        if (reason == "unavailable") menu.IsAvailable = false;
        else menu.Price = 0.001m;
        order.Items.Add(new OrderItem
        {
            Id = 72, OrderId = 7, MenuItemId = 4, MenuItem = menu,
            Quantity = 1, UnitPrice = 50m, DishNameFixed = "Old second"
        });
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        AssertError(async () => await new OrderService(work.Object, Mock.Of<ILoyaltyService>()).ConfirmAsync(7),
            reason == "unavailable" ? RestaurantErrorType.Conflict : RestaurantErrorType.Validation);
        Assert.That(order.Status, Is.EqualTo(OrderStatus.Draft));
        Assert.That(order.DiscountPercentFixed, Is.Null);
        Assert.That(order.Items.First().UnitPrice, Is.EqualTo(80m));
        Assert.That(order.Items.First().DishNameFixed, Is.EqualTo("Old dish"));
        work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("confirm")]
    [TestCase("complete")]
    [TestCase("policy")]
    public async Task RealSqliteTransaction_RollsBackWholeOperationWhenSaveThenFailureOccurs(string operation)
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<RestaurantDbContext>().UseSqlite(connection).Options;
        using var context = new RestaurantDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var levels = TestLevels();
        var order = TestOrder(operation == "confirm" ? OrderStatus.Draft : OrderStatus.Confirmed);
        order.Customer!.FirstName = "Test";
        order.Customer.LastName = "Customer";
        order.Customer.LoyaltyCode = new string('A', 32);
        order.Customer.LoyaltyLevel = levels[1];
        context.LoyaltyLevels.AddRange(levels);
        context.Orders.Add(order);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var actual = new UnitOfWork(context);
        var work = new Mock<IUnitOfWork>();
        work.SetupGet(value => value.Orders).Returns(actual.Orders);
        work.SetupGet(value => value.Customers).Returns(actual.Customers);
        work.SetupGet(value => value.LoyaltyLevels).Returns(actual.LoyaltyLevels);
        work.Setup(value => value.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> action, CancellationToken ct) => actual.ExecuteInTransactionAsync(action, ct));

        work.Setup(value => value.SaveAsync(It.IsAny<CancellationToken>())).Returns(async (CancellationToken ct) =>
        {
            await actual.SaveAsync(ct);
            throw new InvalidOperationException("Test failure after the operation's single Save.");
        });
        var loyalty = new LoyaltyService(work.Object, Mapper);
        var service = new OrderService(work.Object, loyalty);
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            if (operation == "confirm") await service.ConfirmAsync(7);
            else if (operation == "complete") await service.CompleteAsync(7);
            else await loyalty.UpdateLevelsAsync(new UpdateLoyaltyLevelsDto
            {
                Levels = new[]
                {
                    new UpdateLoyaltyLevelDto { Id = 17 },
                    new UpdateLoyaltyLevelDto { Id = 4, MinOrders = 3, MinTotalSpent = 300m, DiscountPercent = 7m },
                    new UpdateLoyaltyLevelDto { Id = 9, MinOrders = 6, MinTotalSpent = 2000m, DiscountPercent = 15m }
                }
            });
        });

        using var reader = new RestaurantDbContext(options);
        var saved = await reader.Orders.Include(value => value.Items).SingleAsync();
        Assert.That(saved.Status, Is.EqualTo(operation == "confirm" ? OrderStatus.Draft : OrderStatus.Confirmed));
        Assert.That(saved.DiscountPercentFixed, Is.EqualTo(operation == "confirm" ? null : (decimal?)5m));
        Assert.That(saved.Items.Single().UnitPrice, Is.EqualTo(80m));
        Assert.That(saved.Items.Single().DishNameFixed, Is.EqualTo("Old dish"));
        Assert.That((await reader.LoyaltyLevels.SingleAsync(level => level.Id == 4)).DiscountPercent, Is.EqualTo(5m));
        Assert.That((await reader.Customers.SingleAsync()).LoyaltyLevelId, Is.EqualTo(4));
        work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
