using Data.Entities;
using Data.Enums;
using Data.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Data.Tests;

public class ConstraintTests : SqliteDatabaseFixture
{
    [TestCase("DishIngredient")]
    [TestCase("OrderItem")]
    [TestCase("Customer")]
    [TestCase("LoyaltyLevel")]
    public async Task UniqueConstraints_RejectDuplicates(string entityName)
    {
        var order = await SaveOrderGraphAsync();
        var item = order.Items.Single();
        switch (entityName)
        {
            case "DishIngredient":
                var link = item.MenuItem.Dish.DishIngredients.Single();
                Context.DishIngredients.Add(new DishIngredient { DishId = link.DishId, IngredientId = link.IngredientId });
                break;
            case "OrderItem":
                Context.OrderItems.Add(new OrderItem
                {
                    OrderId = order.Id, MenuItemId = item.MenuItemId, Quantity = 1, UnitPrice = 10m,
                    DishNameFixed = "Salad", PortionNameFixed = "Regular", PortionDescriptionFixed = "One bowl"
                });
                break;
            case "Customer":
                Context.Customers.Add(new Customer
                {
                    FirstName = "Other", LastName = "Customer", LoyaltyCode = order.Customer!.LoyaltyCode,
                    LoyaltyLevelId = order.Customer.LoyaltyLevelId
                });
                break;
            case "LoyaltyLevel":
                Context.LoyaltyLevels.Add(new LoyaltyLevel { Name = "Basic" });
                break;
        }

        var exception = Assert.ThrowsAsync<DbUpdateException>(async () => await Context.SaveChangesAsync());
        Assert.That(exception!.InnerException, Is.TypeOf<SqliteException>());
        Assert.That(((SqliteException)exception.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(2067));
    }

    [TestCase("DishIngredients", "DishId")]
    [TestCase("DishIngredients", "IngredientId")]
    [TestCase("MenuItems", "DishId")]
    [TestCase("OrderItems", "MenuItemId")]
    [TestCase("OrderItems", "OrderId")]
    [TestCase("Customers", "LoyaltyLevelId")]
    [TestCase("Orders", "CustomerId")]
    public async Task EveryForeignKey_RejectsUnknownPrincipal(string table, string column)
    {
        await SaveOrderGraphAsync();

        var sql = $"UPDATE \"{table}\" SET \"{column}\" = 999999";
        var exception = Assert.ThrowsAsync<SqliteException>(async () =>
            await Context.Database.ExecuteSqlRawAsync(sql));

        Assert.That(exception!.SqliteExtendedErrorCode, Is.EqualTo(787));
        Assert.That(Scalar("PRAGMA foreign_keys"), Is.EqualTo(1L));
    }

    private static IEnumerable<TestCaseData> RequiredColumns()
    {
        var tables = new Dictionary<string, string[]>
        {
            ["Ingredients"] = new[] { "Name" },
            ["Dishes"] = new[] { "Name", "IsAvailable" },
            ["DishIngredients"] = new[] { "DishId", "IngredientId" },
            ["MenuItems"] = new[] { "DishId", "PortionName", "PortionDescription", "Price", "IsAvailable" },
            ["LoyaltyLevels"] = new[] { "Name", "DiscountPercent", "MinOrders", "MinTotalSpent" },
            ["Customers"] = new[] { "FirstName", "LastName", "LoyaltyCode", "LoyaltyLevelId" },
            ["Orders"] = new[] { "CreatedAt", "Status", "OrderType" },
            ["OrderItems"] = new[]
            {
                "OrderId", "MenuItemId", "Quantity", "UnitPrice",
                "DishNameFixed", "PortionNameFixed", "PortionDescriptionFixed"
            }
        };
        foreach (var (table, columns) in tables)
        {
            foreach (var column in columns)
            {
                yield return new TestCaseData(table, column);
            }
        }
    }

    [TestCaseSource(nameof(RequiredColumns))]
    public async Task RequiredColumns_RejectNull(string table, string column)
    {
        await SaveOrderGraphAsync();
        var sql = $"UPDATE \"{table}\" SET \"{column}\" = NULL";
        var exception = Assert.ThrowsAsync<SqliteException>(async () =>
            await Context.Database.ExecuteSqlRawAsync(sql));

        Assert.That(exception!.SqliteExtendedErrorCode, Is.EqualTo(1299));
    }

    [TestCase("MenuItems", "Price", "0")]
    [TestCase("MenuItems", "Price", "-1")]
    [TestCase("MenuItems", "Price", "0.5")]
    [TestCase("MenuItems", "IsAvailable", "2")]
    [TestCase("Dishes", "IsAvailable", "2")]
    [TestCase("OrderItems", "UnitPrice", "0")]
    [TestCase("OrderItems", "UnitPrice", "-1")]
    [TestCase("OrderItems", "UnitPrice", "0.5")]
    [TestCase("OrderItems", "Quantity", "0")]
    [TestCase("OrderItems", "Quantity", "-1")]
    [TestCase("OrderItems", "Quantity", "2147483648")]
    [TestCase("LoyaltyLevels", "DiscountPercent", "-1")]
    [TestCase("LoyaltyLevels", "DiscountPercent", "10001")]
    [TestCase("LoyaltyLevels", "MinOrders", "-1")]
    [TestCase("LoyaltyLevels", "MinOrders", "2147483648")]
    [TestCase("LoyaltyLevels", "MinTotalSpent", "-1")]
    [TestCase("LoyaltyLevels", "MinTotalSpent", "0.5")]
    [TestCase("Orders", "Status", "4")]
    [TestCase("Orders", "OrderType", "3")]
    [TestCase("Orders", "DiscountPercentFixed", "-1")]
    [TestCase("Orders", "DiscountPercentFixed", "10001")]
    public async Task CheckConstraints_RejectInvalidStoredValues(string table, string column, string value)
    {
        await SaveOrderGraphAsync(status: OrderStatus.Confirmed);
        var sql = $"UPDATE \"{table}\" SET \"{column}\" = {value}";
        var exception = Assert.ThrowsAsync<SqliteException>(async () =>
            await Context.Database.ExecuteSqlRawAsync(sql));

        Assert.That(exception!.SqliteExtendedErrorCode, Is.EqualTo(275));
    }

    [TestCase(0, "0", true)]
    [TestCase(1, "NULL", true)]
    [TestCase(2, "NULL", true)]
    [TestCase(1, "500", false)]
    [TestCase(2, "500", false)]
    public async Task OrderChecks_RejectInconsistentStatusDiscountOrGuest(
        int status, string discount, bool withCustomer)
    {
        await SaveOrderGraphAsync(withCustomer);
        var sql = $"UPDATE Orders SET Status = {status}, DiscountPercentFixed = {discount}";
        var exception = Assert.ThrowsAsync<SqliteException>(async () =>
            await Context.Database.ExecuteSqlRawAsync(sql));

        Assert.That(exception!.SqliteExtendedErrorCode, Is.EqualTo(275));
    }

    [TestCase("NULL")]
    [TestCase("500")]
    public async Task CancelledOrder_AllowsUnconfirmedOrConfirmedDiscount(string discount)
    {
        await SaveOrderGraphAsync();
        var sql = $"UPDATE Orders SET Status = 3, DiscountPercentFixed = {discount}";

        Assert.That(await Context.Database.ExecuteSqlRawAsync(sql), Is.EqualTo(1));
    }

    [Test]
    public void PrimaryKeys_RejectExplicitNegativeId()
    {
        var exception = Assert.Throws<SqliteException>(() =>
            Context.Database.ExecuteSqlRaw("INSERT INTO Ingredients (Id, Name) VALUES (-1, 'Invalid')"));

        Assert.That(exception!.SqliteExtendedErrorCode, Is.EqualTo(275));
    }

    [Test]
    public async Task NonuniqueNamesAndPhoneNumbers_AreAllowed()
    {
        var saved = await SaveOrderGraphAsync();
        Context.Ingredients.Add(new Ingredient { Name = "Tomato" });
        Context.Dishes.Add(new Dish { Name = "Salad" });
        saved.Customer!.PhoneNumber = "123";
        Context.Customers.Add(new Customer
        {
            FirstName = "Test", LastName = "Customer", PhoneNumber = "123",
            LoyaltyCode = new string('A', 32), LoyaltyLevelId = saved.Customer.LoyaltyLevelId
        });
        await Context.SaveChangesAsync();
        using var reader = CreateContext();

        Assert.That(await reader.Ingredients.CountAsync(), Is.EqualTo(2));
        Assert.That(await reader.Dishes.CountAsync(), Is.EqualTo(2));
        Assert.That(await reader.Customers.CountAsync(entity => entity.PhoneNumber == "123"), Is.EqualTo(2));
    }
}
