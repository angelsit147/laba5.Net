using AutoMapper;
using Business.Validation;
using Data.Entities;
using Data.Enums;
using Data.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace Business.Tests.Fixtures;

public static class BusinessTestData
{
    private static readonly ILoggerFactory Logging = LoggerFactory.Create(
        builder => builder.AddProvider(new NUnitLogProvider()));

    public static MapperConfiguration Configuration { get; } =
        new(configuration => configuration.AddProfile<AutomapperProfile>(), Logging);

    public static IMapper Mapper { get; } = Configuration.CreateMapper();

    public static IEnumerable<TestCaseData> InvalidNames =>
        new string?[] { null, "", "   ", new string('a', 101) }.Select(value => new TestCaseData(value));

    public static Mock<IUnitOfWork> CreateUnitOfWork()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(value => value.Ingredients).Returns(Mock.Of<IRepository<Ingredient>>());
        unitOfWork.SetupGet(value => value.Dishes).Returns(Mock.Of<IDishRepository>());
        unitOfWork.SetupGet(value => value.MenuItems).Returns(Mock.Of<IMenuItemRepository>());
        unitOfWork.SetupGet(value => value.Customers).Returns(Mock.Of<ICustomerRepository>());
        unitOfWork.SetupGet(value => value.LoyaltyLevels).Returns(Mock.Of<ILoyaltyLevelRepository>());
        unitOfWork.SetupGet(value => value.Orders).Returns(Mock.Of<IOrderRepository>());
        unitOfWork.Setup(value => value.SaveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        unitOfWork.Setup(value => value.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, Task> operation, CancellationToken ct) => operation(ct));
        return unitOfWork;
    }

    public static List<LoyaltyLevel> TestLevels() => new()
    {
        new LoyaltyLevel { Id = 17, Name = "Basic" },
        new LoyaltyLevel { Id = 4, Name = "Silver", MinOrders = 2, MinTotalSpent = 200m, DiscountPercent = 5m },
        new LoyaltyLevel { Id = 9, Name = "Gold", MinOrders = 5, MinTotalSpent = 1000m, DiscountPercent = 10m }
    };

    public static MenuItem TestMenu(int id = 3, decimal price = 100m)
    {
        var dish = new Dish { Id = id, Name = "Current dish", IsAvailable = true };
        dish.DishIngredients.Add(new DishIngredient { IngredientId = 1, Ingredient = new Ingredient { Id = 1, Name = "Salt" } });
        return new MenuItem
        {
            Id = id, DishId = dish.Id, Dish = dish, Price = price, IsAvailable = true,
            PortionName = "Current portion", PortionDescription = "Current description"
        };
    }

    public static Order TestOrder(OrderStatus status = OrderStatus.Draft, bool withCustomer = true)
    {
        var level = TestLevels()[1];
        var order = new Order
        {
            Id = 7, CreatedAt = DateTime.UtcNow, Status = status, CustomerId = withCustomer ? 42 : null,
            Customer = withCustomer ? new Customer { Id = 42, LoyaltyLevelId = level.Id, LoyaltyLevel = level } : null,
            DiscountPercentFixed = status is OrderStatus.Confirmed or OrderStatus.Completed ? (withCustomer ? 5m : 0m) : null
        };
        order.Items.Add(new OrderItem
        {
            Id = 71, OrderId = order.Id, Order = order, MenuItemId = 3, MenuItem = TestMenu(), Quantity = 2,
            UnitPrice = 80m, DishNameFixed = "Old dish", PortionNameFixed = "Old portion", PortionDescriptionFixed = "Old description"
        });
        return order;
    }

    public static void AssertError(AsyncTestDelegate action, RestaurantErrorType type, string? code = null)
    {
        var error = Assert.ThrowsAsync<RestaurantException>(action);
        Assert.That(error!.ErrorType, Is.EqualTo(type));
        Assert.That(error.Code, Is.Not.Empty);
        Assert.That(error.Message, Is.Not.Empty);
        if (code is not null)
        {
            Assert.That(error.Code, Is.EqualTo(code));
        }
    }

    public static void VerifyNoSave(Mock<IUnitOfWork> unitOfWork)
    {
        unitOfWork.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(value => value.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }


    private sealed class NUnitLogProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new NUnitLogger(categoryName);
        public void Dispose() { }
    }

    private sealed class NUnitLogger : ILogger
    {
        private readonly string category;

        public NUnitLogger(string category)
        {
            this.category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            TestContext.Progress.WriteLine($"{logLevel} {category}: {formatter(state, exception)}");
        }
    }
}
