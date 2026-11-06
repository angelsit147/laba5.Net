using System.Globalization;
using Data.Entities;
using Data.Enums;
using Data.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Data.Tests;

public class PersistenceTests : SqliteDatabaseFixture
{
    [TestCase("0.01")]
    [TestCase("123.45")]
    [TestCase("92233720368547758.07")]
    public async Task Money_RoundTripsAsIntegerCentsWithoutPrecisionLoss(string text)
    {
        var value = decimal.Parse(text, CultureInfo.InvariantCulture);
        var order = await SaveOrderGraphAsync();
        order.Items.Single().MenuItem.Price = value;
        order.Items.Single().UnitPrice = value;
        order.Customer!.LoyaltyLevel.MinTotalSpent = value;
        await Context.SaveChangesAsync();

        Assert.That(Scalar("SELECT Price FROM MenuItems"), Is.EqualTo(checked((long)(value * 100m))));
        Assert.That(Scalar("SELECT typeof(Price) FROM MenuItems"), Is.EqualTo("integer"));
        Assert.That(Scalar("SELECT UnitPrice FROM OrderItems"), Is.EqualTo(checked((long)(value * 100m))));
        Assert.That(Scalar("SELECT MinTotalSpent FROM LoyaltyLevels"), Is.EqualTo(checked((long)(value * 100m))));
        using var reader = CreateContext();
        Assert.That((await reader.MenuItems.SingleAsync()).Price, Is.EqualTo(value));
        Assert.That((await reader.OrderItems.SingleAsync()).UnitPrice, Is.EqualTo(value));
        Assert.That((await reader.LoyaltyLevels.SingleAsync()).MinTotalSpent, Is.EqualTo(value));
    }

    [TestCase("0")]
    [TestCase("7.25")]
    [TestCase("100")]
    public async Task Percent_RoundTripsAsHundredthsIncludingNullableFixed(string text)
    {
        var value = decimal.Parse(text, CultureInfo.InvariantCulture);
        var order = await SaveOrderGraphAsync();
        order.Customer!.LoyaltyLevel.DiscountPercent = value;
        order.Status = OrderStatus.Confirmed;
        order.DiscountPercentFixed = value;
        await Context.SaveChangesAsync();

        Assert.That(Scalar("SELECT DiscountPercent FROM LoyaltyLevels"), Is.EqualTo((long)(value * 100m)));
        Assert.That(Scalar("SELECT typeof(DiscountPercentFixed) FROM Orders"), Is.EqualTo("integer"));
        Assert.That(Scalar("SELECT DiscountPercentFixed FROM Orders"), Is.EqualTo((long)(value * 100m)));
        using var reader = CreateContext();
        Assert.That((await reader.LoyaltyLevels.SingleAsync()).DiscountPercent, Is.EqualTo(value));
        Assert.That((await reader.Orders.SingleAsync()).DiscountPercentFixed, Is.EqualTo(value));
    }

    [TestCase("0.001")]
    [TestCase("-0.01")]
    [TestCase("92233720368547758.08")]
    public async Task InvalidMoney_IsRejectedWithoutRoundingOrWriting(string text)
    {
        var order = await SaveOrderGraphAsync();
        order.Items.Single().MenuItem.Price = decimal.Parse(text, CultureInfo.InvariantCulture);

        var exception = Assert.CatchAsync(async () => await Context.SaveChangesAsync());

        Assert.That(exception!.GetBaseException(), Is.InstanceOf<ArgumentException>());
        using var reader = CreateContext();
        Assert.That((await reader.MenuItems.SingleAsync()).Price, Is.EqualTo(123.45m));
    }

    [TestCase("7.251")]
    [TestCase("-0.01")]
    [TestCase("100.01")]
    public async Task InvalidPercent_IsRejectedWithoutWriting(string text)
    {
        var order = await SaveOrderGraphAsync();
        order.Customer!.LoyaltyLevel.DiscountPercent = decimal.Parse(text, CultureInfo.InvariantCulture);

        var exception = Assert.CatchAsync(async () => await Context.SaveChangesAsync());

        Assert.That(exception!.GetBaseException(), Is.InstanceOf<ArgumentException>());
        using var reader = CreateContext();
        Assert.That((await reader.LoyaltyLevels.SingleAsync()).DiscountPercent, Is.Zero);
    }

    [Test]
    public async Task UtcTimeAndHistoricalFields_RoundTripExactly()
    {
        var order = await SaveOrderGraphAsync(status: OrderStatus.Completed);
        order.CreatedAt = order.CreatedAt.AddTicks(4567);
        order.Items.Single().DishNameFixed = "Historical dish";
        order.Items.Single().MenuItem.Dish.Name = "New catalog name";
        await Context.SaveChangesAsync();
        using var reader = CreateContext();
        var loaded = await reader.Orders.Include(entity => entity.Items).SingleAsync();

        Assert.That(loaded.CreatedAt, Is.EqualTo(order.CreatedAt));
        Assert.That(loaded.CreatedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(Scalar("SELECT typeof(CreatedAt) FROM Orders"), Is.EqualTo("text"));
        Assert.That(loaded.Items.Single().DishNameFixed, Is.EqualTo("Historical dish"));
        Assert.That(loaded.Items.Single().UnitPrice, Is.EqualTo(123.45m));
    }

    [TestCase(DateTimeKind.Local)]
    [TestCase(DateTimeKind.Unspecified)]
    public void NonUtcTime_IsRejected(DateTimeKind kind)
    {
        Context.Orders.Add(new Order { CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, kind) });

        var exception = Assert.CatchAsync(async () => await Context.SaveChangesAsync());

        Assert.That(exception!.GetBaseException(), Is.InstanceOf<ArgumentException>());
        using var reader = CreateContext();
        Assert.That(reader.Orders.Count(), Is.Zero);
    }

    [TestCase(OrderStatus.Draft, OrderType.DineIn)]
    [TestCase(OrderStatus.Confirmed, OrderType.Takeaway)]
    [TestCase(OrderStatus.Completed, OrderType.Delivery)]
    [TestCase(OrderStatus.Cancelled, OrderType.Delivery)]
    public async Task EnumValues_AreStableIntegers(OrderStatus status, OrderType type)
    {
        var order = await SaveOrderGraphAsync(status: status);
        order.OrderType = type;
        await Context.SaveChangesAsync();

        Assert.That(Scalar("SELECT Status FROM Orders"), Is.EqualTo((long)status));
        Assert.That(Scalar("SELECT OrderType FROM Orders"), Is.EqualTo((long)type));
        using var reader = CreateContext();
        Assert.That((await reader.Orders.SingleAsync()).OrderType, Is.EqualTo(type));
    }

    [Test]
    public void Schema_HasExactlyEightIndependentTablesAndSevenForeignKeys()
    {
        var entities = Context.Model.GetEntityTypes().ToList();
        var tableNames = entities.Select(entity => entity.GetTableName()).ToList();
        var foreignKeys = entities.SelectMany(entity => entity.GetForeignKeys()).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(tableNames, Is.EquivalentTo(new[]
            {
                "Ingredients", "Dishes", "DishIngredients", "MenuItems",
                "LoyaltyLevels", "Customers", "Orders", "OrderItems"
            }));
            Assert.That(entities, Has.Count.EqualTo(8));
            Assert.That(entities.All(entity => entity.BaseType is null), Is.True);
            Assert.That(entities.All(entity => entity.FindDiscriminatorProperty() is null), Is.True);
            Assert.That(entities.All(entity => entity.FindPrimaryKey()!.Properties.Single().Name == "Id"), Is.True);
            Assert.That(entities.All(entity => typeof(BaseEntity).IsAssignableFrom(entity.ClrType)), Is.True);
            Assert.That(foreignKeys, Has.Count.EqualTo(7));
            Assert.That(foreignKeys.Count(fk => fk.DeleteBehavior == DeleteBehavior.Restrict), Is.EqualTo(6));
            Assert.That(foreignKeys.Single(fk => !fk.IsRequired).DeleteBehavior, Is.EqualTo(DeleteBehavior.ClientNoAction));
            Assert.That(Scalar("SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'"), Is.EqualTo(8L));
        });
        var nullable = entities.SelectMany(entity => entity.GetProperties()
            .Where(property => property.IsNullable)
            .Select(property => entity.ClrType.Name + "." + property.Name));
        Assert.That(nullable, Is.EquivalentTo(new[]
        {
            "Dish.Description", "Customer.PhoneNumber", "Order.CustomerId", "Order.DiscountPercentFixed"
        }));
        using var command = Context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA foreign_key_list(Orders)";
        using var reader = command.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader["on_delete"], Is.EqualTo("NO ACTION"));
    }

    [Test]
    public void Indexes_MatchDocumentedUniqueAndForeignKeyIndexes()
    {
        var indexes = Context.Model.GetEntityTypes().SelectMany(entity => entity.GetIndexes()).ToList();
        var unique = indexes.Where(index => index.IsUnique).Select(index => index.GetDatabaseName());
        Assert.That(unique, Is.EquivalentTo(new[]
        {
            "UX_DishIngredients_DishId_IngredientId", "UX_OrderItems_OrderId_MenuItemId",
            "UX_Customers_LoyaltyCode", "UX_LoyaltyLevels_Name"
        }));
        Assert.That(indexes.Select(index => index.GetDatabaseName()), Is.EquivalentTo(new[]
        {
            "UX_DishIngredients_DishId_IngredientId", "IX_DishIngredients_IngredientId", "IX_MenuItems_DishId",
            "UX_LoyaltyLevels_Name", "UX_Customers_LoyaltyCode", "IX_Customers_LoyaltyLevelId",
            "IX_Orders_CustomerId_Status", "UX_OrderItems_OrderId_MenuItemId", "IX_OrderItems_MenuItemId"
        }));
        Assert.That(Scalar("SELECT count(*) FROM sqlite_master WHERE type = 'index' AND name NOT LIKE 'sqlite_%'"), Is.EqualTo(9L));
    }
}
