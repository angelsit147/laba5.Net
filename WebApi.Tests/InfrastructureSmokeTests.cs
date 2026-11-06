using System.Text.Json;
using Business.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public class InfrastructureSmokeTests : ApiTestBase
{
    [Test]
    public async Task Application_StartsAndServesSwagger()
    {
        var document = await Get<JsonElement>("/swagger/v1/swagger.json");
        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.That(schemas.GetProperty("OrderStatus").GetProperty("type").GetString(), Is.EqualTo("string"));
        Assert.That(schemas.GetProperty("OrderType").GetProperty("type").GetString(), Is.EqualTo("string"));
        using var ui = await Client.GetAsync("/swagger/index.html");
        Assert.That(await ui.Content.ReadAsStringAsync(), Does.Contain("Swagger UI"));
        using var scope = Factory.Services.CreateScope();
        foreach (var type in new[] { typeof(IIngredientService), typeof(IDishService), typeof(IMenuService),
            typeof(ICustomerService), typeof(ILoyaltyService), typeof(IOrderService) })
            Assert.That(scope.ServiceProvider.GetRequiredService(type), Is.Not.Null);
    }

    [Test]
    public async Task Migration_CreatesAgreedSchemaAndIsIdempotent()
    {
        await Factory.ReadAsync(async context =>
        {
            await context.Database.MigrateAsync();
            Assert.That((await context.Database.GetAppliedMigrationsAsync()).Count(), Is.EqualTo(1));
            await context.Database.OpenConnectionAsync();
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name != '__EFMigrationsHistory'";
            var tables = new List<string>();
            using (var reader = await command.ExecuteReaderAsync())
                while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
            Assert.That(tables, Is.EquivalentTo(new[] { "Ingredients", "Dishes", "DishIngredients", "MenuItems",
                "LoyaltyLevels", "Customers", "Orders", "OrderItems" }));
            var fkCount = 0;
            var uniqueCount = 0;
            foreach (var table in tables)
            {
                command.CommandText = $"PRAGMA foreign_key_list(\"{table}\")";
                using (var reader = await command.ExecuteReaderAsync())
                    while (await reader.ReadAsync()) fkCount++;
                command.CommandText = $"PRAGMA index_list(\"{table}\")";
                using (var reader = await command.ExecuteReaderAsync())
                    while (await reader.ReadAsync()) uniqueCount += reader.GetInt32(2);
            }
            Assert.That(fkCount, Is.EqualTo(7));
            Assert.That(uniqueCount, Is.EqualTo(4));
            var nullable = new List<string>();
            foreach (var table in tables)
            {
                command.CommandText = $"PRAGMA table_info(\"{table}\")";
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (reader.GetInt32(3) == 0 && reader.GetInt32(5) == 0) nullable.Add($"{table}.{name}");
                    if (name is "Price" or "UnitPrice" or "MinTotalSpent" or "DiscountPercent" or "DiscountPercentFixed")
                        Assert.That(reader.GetString(2), Is.EqualTo("INTEGER"));
                }
            }
            Assert.That(nullable, Is.EquivalentTo(new[] { "Dishes.Description", "Customers.PhoneNumber",
                "Orders.CustomerId", "Orders.DiscountPercentFixed" }));
            command.CommandText = "SELECT sql FROM sqlite_master WHERE name='Orders'";
            Assert.That((string)(await command.ExecuteScalarAsync())!, Does.Contain("CHECK").And.Contain("DiscountPercentFixed"));
            command.CommandText = "PRAGMA foreign_keys";
            Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(1));
            return true;
        });
        await Factory.SeedAgainAsync();
        Assert.That(await Factory.ReadAsync(context => context.LoyaltyLevels.CountAsync()), Is.EqualTo(3));
    }
}
