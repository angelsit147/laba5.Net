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

public class LoyaltyServiceTests
{
    private Mock<IUnitOfWork> work = null!;
    private List<LoyaltyLevel> levels = null!;
    private Customer customer = null!;
    private LoyaltyService service = null!;
    private readonly CancellationToken ct = new CancellationTokenSource().Token;

    [SetUp]
    public void SetUp()
    {
        work = CreateUnitOfWork();
        levels = TestLevels();
        customer = new Customer { Id = 42, LoyaltyLevelId = 17, LoyaltyLevel = levels[0] };
        Mock.Get(work.Object.LoyaltyLevels).Setup(value => value.GetAllAsync(ct)).ReturnsAsync(levels);
        Mock.Get(work.Object.LoyaltyLevels).Setup(value => value.GetByIdAsync(It.IsAny<int>(), ct))
            .ReturnsAsync((int id, CancellationToken _) => levels.SingleOrDefault(level => level.Id == id));
        Mock.Get(work.Object.Customers).Setup(value => value.GetByIdAsync(42, ct)).ReturnsAsync(customer);
        Mock.Get(work.Object.Customers).Setup(value => value.GetAllWithLevelsAsync(ct)).ReturnsAsync(new[] { customer });
        Mock.Get(work.Object.Orders).Setup(value => value.GetCompletedByCustomerAsync(42, ct)).ReturnsAsync(Array.Empty<Order>());
        Mock.Get(work.Object.Orders).Setup(value => value.GetCompletedForCustomersAsync(
            It.IsAny<IReadOnlyCollection<int>>(), ct)).ReturnsAsync(Array.Empty<Order>());
        service = new LoyaltyService(work.Object, Mapper);
    }

    private static Order Completed(int id, decimal price, int customerId = 42, decimal discount = 0m) => new()
    {
        Id = id, CustomerId = customerId, Status = OrderStatus.Completed, DiscountPercentFixed = discount,
        Items = new List<OrderItem> { new() { Quantity = 1, UnitPrice = price } }
    };

    private UpdateLoyaltyLevelsDto Updates() => new()
    {
        Levels = levels.Select(level => new UpdateLoyaltyLevelDto
        {
            Id = level.Id, DiscountPercent = level.DiscountPercent,
            MinOrders = level.MinOrders, MinTotalSpent = level.MinTotalSpent
        }).ToList()
    };

    [TestCase(1, "1000", "Basic", false)]
    [TestCase(2, "199.99", "Basic", false)]
    [TestCase(2, "200", "Silver", false)]
    [TestCase(5, "999.99", "Silver", false)]
    [TestCase(5, "1000", "Gold", false)]
    [TestCase(6, "1500", "Platinum", true)]
    public async Task ChoosesHighestLevelUsingBothInclusiveThresholds(int count, string total, string name, bool fourth)
    {
        if (fourth)
        {
            levels.Add(new LoyaltyLevel { Id = 2, Name = "Platinum", MinOrders = 6, MinTotalSpent = 1500m, DiscountPercent = 15m });
        }

        var spent = decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture);
        var firstPrice = decimal.Floor(spent / count * 100m) / 100m;
        var history = Enumerable.Range(1, count - 1).Select(id => Completed(id, firstPrice)).ToArray();
        var current = Completed(100, spent - firstPrice * (count - 1));
        Mock.Get(work.Object.Orders).Setup(value => value.GetCompletedByCustomerAsync(42, ct)).ReturnsAsync(history);

        await service.RecalculateCustomerAsync(42, current, ct);

        Assert.That(customer.LoyaltyLevel.Name, Is.EqualTo(name));
        VerifyNoSave(work);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PendingCompleted_IsCountedExactlyOnceEvenIfAlreadyInQuery(bool included)
    {
        levels[2].MinOrders = 3;
        levels[2].MinTotalSpent = 300m;
        var current = Completed(100, 100m);
        var history = new List<Order> { Completed(1, 100m) };
        if (included) history.Add(current);
        Mock.Get(work.Object.Orders).Setup(value => value.GetCompletedByCustomerAsync(42, ct)).ReturnsAsync(history);

        await service.RecalculateCustomerAsync(42, current, ct);

        Assert.That(customer.LoyaltyLevel.Name, Is.EqualTo("Silver"));
        VerifyNoSave(work);
    }

    [Test]
    public async Task Recalculation_UsesHistoricalDiscountAndFiltersOtherStatusesAndGuests()
    {
        var current = Completed(100, 100m, discount: 50m);
        var history = new List<Order> { Completed(1, 100m, discount: 50m) };
        foreach (var status in new[] { OrderStatus.Draft, OrderStatus.Confirmed, OrderStatus.Cancelled })
        {
            var excluded = Completed(history.Count + 1, 10000m);
            excluded.Status = status;
            history.Add(excluded);
        }

        var guest = Completed(20, 10000m);
        guest.CustomerId = null;
        history.Add(guest);
        history.Add(Completed(21, 10000m, 999));
        Mock.Get(work.Object.Orders).Setup(value => value.GetCompletedByCustomerAsync(42, ct)).ReturnsAsync(history);
        await service.RecalculateCustomerAsync(42, current, ct);

        Assert.That(customer.LoyaltyLevel.Name, Is.EqualTo("Basic"));
        Assert.That(current.DiscountPercentFixed, Is.EqualTo(50m));
        VerifyNoSave(work);
    }

    [Test]
    public async Task Reads_ReturnSortedDtosWithoutSaving()
    {
        var all = await service.GetAllAsync(ct);
        Assert.That(all.Select(level => level.Name), Is.EqualTo(new[] { "Basic", "Silver", "Gold" }));
        Assert.That((await service.GetByIdAsync(4, ct)).Name, Is.EqualTo("Silver"));
        AssertError(async () => await service.GetByIdAsync(999, ct), RestaurantErrorType.NotFound);
        AssertError(async () => await service.GetByIdAsync(0, ct), RestaurantErrorType.Validation);
        Mock.Get(work.Object.LoyaltyLevels).Setup(value => value.GetAllAsync(ct)).ReturnsAsync(Array.Empty<LoyaltyLevel>());
        Assert.That(await service.GetAllAsync(ct), Is.Empty);
        VerifyNoSave(work);
    }

    [Test]
    public async Task UpdatePolicy_RecalculatesAllCustomersIncludingPromotionDemotionAndNoHistory()
    {
        customer.LoyaltyLevelId = 9;
        customer.LoyaltyLevel = levels[2];
        var second = new Customer { Id = 43, LoyaltyLevelId = 17, LoyaltyLevel = levels[0] };
        var third = new Customer { Id = 44, LoyaltyLevelId = 9, LoyaltyLevel = levels[2] };
        Mock.Get(work.Object.Customers).Setup(value => value.GetAllWithLevelsAsync(ct)).ReturnsAsync(new[] { customer, second, third });
        var history = new List<Order> { Completed(1, 100m), Completed(2, 100m) };
        history.AddRange(Enumerable.Range(10, 5).Select(id => Completed(id, 200m, 43)));
        Mock.Get(work.Object.Orders).Setup(value => value.GetCompletedForCustomersAsync(
            It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 3 && ids.Contains(44)), ct)).ReturnsAsync(history);
        var dto = Updates();
        dto.Levels.Single(level => level.Id == 4).MinOrders = 3;
        dto.Levels.Single(level => level.Id == 4).MinTotalSpent = 300m;
        dto.Levels.Single(level => level.Id == 4).DiscountPercent = 7.25m;
        dto.Levels.Single(level => level.Id == 9).MinOrders = 6;
        dto.Levels.Single(level => level.Id == 9).MinTotalSpent = 2000m;

        var result = await service.UpdateLevelsAsync(dto, ct);

        Assert.That(customer.LoyaltyLevel.Name, Is.EqualTo("Basic"));
        Assert.That(second.LoyaltyLevel.Name, Is.EqualTo("Silver"));
        Assert.That(third.LoyaltyLevel.Name, Is.EqualTo("Basic"));
        Assert.That(result.Single(level => level.Id == 4).DiscountPercent, Is.EqualTo(7.25m));
        Assert.That(history.All(order => order.DiscountPercentFixed == 0 && order.Items.Single().UnitPrice > 0), Is.True);
        work.Verify(value => value.SaveAsync(ct), Times.Once);
        work.Verify(value => value.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), ct), Times.Once);
    }

    [TestCase("duplicate")]
    [TestCase("missing")]
    [TestCase("empty")]
    [TestCase("basic")]
    [TestCase("negative_orders")]
    [TestCase("negative_spent")]
    [TestCase("money_scale")]
    [TestCase("percent_scale")]
    [TestCase("percent_range")]
    [TestCase("crossed")]
    [TestCase("identical")]
    [TestCase("decreasing_discount")]
    public void InvalidPolicy_IsRejectedBeforeAnyMutation(string reason)
    {
        var dto = Updates();
        var updates = dto.Levels.ToList();
        var silver = updates.Single(level => level.Id == 4);
        var gold = updates.Single(level => level.Id == 9);
        switch (reason)
        {
            case "duplicate": updates.Add(updates[0]); break;
            case "missing": updates.RemoveAt(1); break;
            case "empty": updates.Clear(); break;
            case "basic": updates[0].DiscountPercent = 1m; break;
            case "negative_orders": silver.MinOrders = -1; break;
            case "negative_spent": silver.MinTotalSpent = -1m; break;
            case "money_scale": silver.MinTotalSpent = 0.001m; break;
            case "percent_scale": silver.DiscountPercent = 5.001m; break;
            case "percent_range": silver.DiscountPercent = 101m; break;
            case "crossed": silver.MinOrders = 6; break;
            case "identical": silver.MinOrders = gold.MinOrders; silver.MinTotalSpent = gold.MinTotalSpent; break;
            case "decreasing_discount": silver.DiscountPercent = 20m; break;
        }

        dto.Levels = updates;
        AssertError(async () => await service.UpdateLevelsAsync(dto, ct), RestaurantErrorType.Validation);
        Assert.That(levels[1].MinOrders, Is.EqualTo(2));
        Assert.That(levels[1].DiscountPercent, Is.EqualTo(5m));
        Assert.That(customer.LoyaltyLevelId, Is.EqualTo(17));
        work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void UnknownLevelAndNullBatch_AreRejected()
    {
        var dto = Updates();
        dto.Levels[1].Id = 999;
        AssertError(async () => await service.UpdateLevelsAsync(dto, ct), RestaurantErrorType.NotFound);
        AssertError(async () => await service.UpdateLevelsAsync(null!, ct), RestaurantErrorType.Validation);
        work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Complete_UsesRealLoyaltyLogicOnceAndKeepsOldDiscount()
    {
        var order = TestOrder(OrderStatus.Confirmed);
        order.Customer = customer;
        order.Items.Single().Quantity = 1;
        order.Items.Single().UnitPrice = 100m;
        order.DiscountPercentFixed = 0m;
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, true, ct)).ReturnsAsync(order);
        Mock.Get(work.Object.Orders).Setup(value => value.GetCompletedByCustomerAsync(42, ct))
            .ReturnsAsync(new[] { Completed(1, 100m) });

        var result = await new OrderService(work.Object, service).CompleteAsync(7, ct);

        Assert.That(result.Status, Is.EqualTo(OrderStatus.Completed));
        Assert.That(customer.LoyaltyLevel.Name, Is.EqualTo("Silver"));
        Assert.That(result.DiscountPercentFixed, Is.Zero);
        Assert.That(result.Total, Is.EqualTo(100m));
        work.Verify(value => value.SaveAsync(ct), Times.Once);
        work.Verify(value => value.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), ct), Times.Once);
    }

    [Test]
    public void RecalculationFailure_StopsCompleteBeforeSave()
    {
        var order = TestOrder(OrderStatus.Confirmed);
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, true, ct)).ReturnsAsync(order);
        var loyalty = new Mock<ILoyaltyService>();
        loyalty.Setup(value => value.RecalculateCustomerAsync(42, order, ct)).ThrowsAsync(new InvalidOperationException("Recalculation failed"));

        Assert.ThrowsAsync<InvalidOperationException>(async () => await new OrderService(work.Object, loyalty.Object).CompleteAsync(7, ct));
        work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void PolicyUpdate_DoesNotPermitNameOrIdChangesThroughNewFields()
    {
        Assert.That(typeof(UpdateLoyaltyLevelDto).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "Id", "MinOrders", "MinTotalSpent", "DiscountPercent" }));
        var basic = TestLevels()[0];
        basic.Name = "Renamed";
        AssertError(() => Task.FromResult(LoyaltyPolicyValidator.ValidateAndOrder(new[] { basic })),
            RestaurantErrorType.Validation);
    }

    [TestCase("null_collection")]
    [TestCase("empty")]
    [TestCase("null_level")]
    [TestCase("nonpositive_id")]
    [TestCase("duplicate_id")]
    [TestCase("null_name")]
    [TestCase("empty_name")]
    [TestCase("long_name")]
    [TestCase("duplicate_name")]
    public void InvalidPolicyIdentity_IsRejectedForDirectBusinessCalls(string reason)
    {
        var policy = TestLevels();
        switch (reason)
        {
            case "null_collection": policy = null!; break;
            case "empty": policy.Clear(); break;
            case "null_level": policy[1] = null!; break;
            case "nonpositive_id": policy[1].Id = 0; break;
            case "duplicate_id": policy[1].Id = policy[0].Id; break;
            case "null_name": policy[1].Name = null!; break;
            case "empty_name": policy[1].Name = " "; break;
            case "long_name": policy[1].Name = new string('a', 101); break;
            case "duplicate_name": policy[1].Name = policy[0].Name; break;
        }

        var error = Assert.Throws<RestaurantException>(() => LoyaltyPolicyValidator.ValidateAndOrder(policy));
        Assert.That(error!.ErrorType, Is.EqualTo(RestaurantErrorType.Validation));
        Assert.That(error.Code, Is.EqualTo("invalid_loyalty_policy"));
        Assert.That(error.Message, Is.EqualTo("Loyalty levels must have unique positive Ids and unique nonempty names."));
    }
}
