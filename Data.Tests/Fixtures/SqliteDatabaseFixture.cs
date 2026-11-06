using Data.Data;
using Data.Entities;
using Data.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Data.Tests.Fixtures;

public abstract class SqliteDatabaseFixture
{
    private SqliteConnection connection = null!;
    private DbContextOptions<RestaurantDbContext> options = null!;

    protected RestaurantDbContext Context { get; private set; } = null!;

    [SetUp]
    public void SetUpDatabase()
    {
        connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        options = new DbContextOptionsBuilder<RestaurantDbContext>().UseSqlite(connection).Options;
        Context = CreateContext();
        Context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDownDatabase()
    {
        Context.Dispose();
        connection.Dispose();
    }

    protected RestaurantDbContext CreateContext()
    {
        return new RestaurantDbContext(options);
    }

    protected object? Scalar(string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    protected static Order CreateOrderGraph(bool withCustomer = true, OrderStatus status = OrderStatus.Draft)
    {
        var ingredient = new Ingredient { Name = "Tomato" };
        var dish = new Dish { Name = "Salad", IsAvailable = true };
        dish.DishIngredients.Add(new DishIngredient { Dish = dish, Ingredient = ingredient });
        var menu = new MenuItem
        {
            Dish = dish, PortionName = "Regular", PortionDescription = "One bowl",
            Price = 123.45m, IsAvailable = true
        };
        var order = new Order
        {
            CreatedAt = new DateTime(2026, 1, 15, 12, 30, 45, 123, DateTimeKind.Utc),
            Status = status,
            OrderType = OrderType.DineIn,
            DiscountPercentFixed = status is OrderStatus.Confirmed or OrderStatus.Completed ? 0m : null,
            Customer = withCustomer ? new Customer
            {
                FirstName = "Test", LastName = "Customer",
                LoyaltyCode = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                LoyaltyLevel = new LoyaltyLevel { Name = "Basic" }
            } : null
        };
        order.Items.Add(new OrderItem
        {
            Order = order, MenuItem = menu, Quantity = 2, UnitPrice = 123.45m,
            DishNameFixed = "Salad", PortionNameFixed = "Regular", PortionDescriptionFixed = "One bowl"
        });
        return order;
    }

    protected async Task<Order> SaveOrderGraphAsync(bool withCustomer = true, OrderStatus status = OrderStatus.Draft)
    {
        var order = CreateOrderGraph(withCustomer, status);
        Context.Orders.Add(order);
        await Context.SaveChangesAsync();
        return order;
    }
}
