using System.Net;
using System.Text;
using System.Text.Json;
using Business.Interfaces;
using Data.Data;
using Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public class ErrorContractTests : ApiTestBase
{
    [TestCase("/api/orders", "{")]
    [TestCase("/api/orders", "{}", "invalid_request")]
    [TestCase("/api/orders", "{\"orderType\":\"Unknown\"}")]
    [TestCase("/api/orders", "{\"orderType\":1}")]
    [TestCase("/api/orders", "{\"orderType\":\"1\"}")]
    [TestCase("/api/orders", "{\"orderType\":null}")]
    [TestCase("/api/orders", "{\"orderType\":\"DineIn\",\"status\":\"Completed\"}")]
    [TestCase("/api/orders", "{\"orderType\":\"DineIn\",\"customerId\":1}")]
    [TestCase("/api/orders", "{\"orderType\":\"DineIn\",\"discountPercentFixed\":100}")]
    [TestCase("/api/orders/1/items", "{\"menuItemId\":1,\"quantity\":1,\"unitPrice\":0.01}")]
    [TestCase("/api/dishes", "{\"name\":\"Test\",\"ingredientIds\":[]}", "invalid_dish_ingredients")]
    [TestCase("/api/dishes", "{\"name\":\"Test\",\"isAvailable\":null,\"ingredientIds\":[]}", "invalid_dish_ingredients")]
    [TestCase("/api/dishes", "{\"name\":\"Test\",\"isAvailable\":false}")]
    [TestCase("/api/menu", "{\"dishId\":1,\"portionName\":\"Test\",\"portionDescription\":\"Test\",\"price\":1}", "invalid_menu_item")]
    [TestCase("/api/menu", "{\"dishId\":1,\"portionName\":\"Test\",\"portionDescription\":\"Test\",\"price\":1,\"isAvailable\":null}")]
    [TestCase("/api/ingredients", "{\"name\":null}", "invalid_request")]
    [TestCase("/api/orders", "null")]
    [TestCase("/api/ingredients", "{}", "invalid_name")]
    [TestCase("/api/customers", "{\"lastName\":\"Test\"}", "invalid_name")]
    [TestCase("/api/customers/resolve-loyalty", "{}", "invalid_loyalty_code")]
    [TestCase("/api/orders/1/items", "{\"menuItemId\":1}", "invalid_quantity")]
    [TestCase("/api/orders/1/items", "{\"menuItemId\":0,\"quantity\":1}", "invalid_id")]
    [TestCase("/api/orders/1/items/1", "{}", "invalid_quantity", "PUT")]
    [TestCase("/api/customers", "{\"firstName\":\"Test\",\"lastName\":\"Customer\",\"loyaltyLevelId\":3}", "invalid_request")]
    [TestCase("/api/orders/1", "{\"orderType\":\"DineIn\",\"status\":\"Completed\"}", "invalid_request", "PUT")]
    [TestCase("/api/orders/1/items", "{\"menuItemId\":1,\"quantity\":2147483648}", "invalid_request")]
    [TestCase("/api/menu", "{\"dishId\":1,\"price\":\"NaN\",\"isAvailable\":true}", "invalid_request")]
    [TestCase("/api/menu", "{\"dishId\":1,\"price\":79228162514264337593543950336,\"isAvailable\":true}", "invalid_request")]
    [TestCase("/api/ingredients", "{\"name\":\"Test\",\"$type\":\"System.IO.FileInfo\"}", "invalid_request")]
    public async Task InvalidJsonOrWriteShape_ReturnsSafe400Problem(
        string path, string json, string? expectedCode = null, string method = "POST")
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        using var response = await Client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await response.Content.ReadAsStringAsync());
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("status").GetInt32(), Is.EqualTo(400));
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.Not.Empty);
        if (expectedCode is not null)
            Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo(expectedCode));
        Assert.That(await Factory.ReadAsync(context => context.Orders.CountAsync()), Is.Zero);
    }

    [Test]
    public async Task MissingPolicyNumber_IsRejectedInsteadOfDefaultingToZero()
    {
        using var content = new StringContent("{\"levels\":[{\"id\":1,\"minOrders\":0,\"minTotalSpent\":0}]}",
            Encoding.UTF8, "application/json");
        using var response = await Client.PutAsync("/api/loyalty-levels", content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo("invalid_request"));
    }

    [TestCase("validation", 400)]
    [TestCase("not_found", 404)]
    [TestCase("conflict", 409)]
    public async Task BusinessErrors_HaveProblemDetailsAndMachineCode(string kind, int status)
    {
        HttpResponseMessage response;
        if (kind == "validation")
            response = await Client.PostAsync("/api/ingredients", new StringContent("{\"name\":\"\"}", Encoding.UTF8, "application/json"));
        else if (kind == "not_found")
            response = await Client.GetAsync("/api/ingredients/999");
        else
        {
            var catalog = await Catalog();
            response = await Client.DeleteAsync($"/api/ingredients/{catalog.Dish.Ingredients[0].Id}");
        }
        using (response)
        {
            Assert.That((int)response.StatusCode, Is.EqualTo(status));
            Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.That(document.RootElement.GetProperty("status").GetInt32(), Is.EqualTo(status));
            Assert.That(document.RootElement.GetProperty("code").GetString(), Is.Not.Empty);
        }
    }

    [TestCase("unique", 409, "unique_conflict")]
    [TestCase("fk", 409, "foreign_key_conflict")]
    [TestCase("fk_restrict", 409, "foreign_key_conflict")]
    [TestCase("trigger", 500, "internal_error")]
    [TestCase("other_trigger_message", 500, "internal_error")]
    [TestCase("busy", 409, "storage_busy")]
    [TestCase("locked", 409, "storage_busy")]
    [TestCase("check", 500, "internal_error")]
    [TestCase("other_db", 500, "internal_error")]
    [TestCase("other_sqlite", 500, "internal_error")]
    [TestCase("unexpected", 500, "internal_error")]
    public async Task ProviderAndUnexpectedErrors_AreClassifiedWithoutLeakingDetails(string kind, int status, string code)
    {
        using var factory = new RestaurantWebApplicationFactory(services =>
        {
            services.RemoveAll<IIngredientService>();
            services.AddScoped<IIngredientService>(provider =>
            {
                var context = provider.GetRequiredService<RestaurantDbContext>();
                if (kind == "fk_restrict")
                {
                    var ingredient = new Ingredient { Name = "Test confidential data" };
                    var dish = new Dish { Name = "Test dish" };
                    dish.DishIngredients.Add(new DishIngredient { Ingredient = ingredient });
                    context.Dishes.Add(dish);
                    context.SaveChanges();
                    context.ChangeTracker.Clear();
                    context.Ingredients.Remove(context.Ingredients.Single());
                }
                if (kind == "trigger")
                {
                    context.Database.ExecuteSqlRaw("""
                        CREATE TRIGGER RejectAuditIngredient BEFORE INSERT ON Ingredients
                        BEGIN SELECT RAISE(ABORT, 'Test confidential data'); END;
                        """);
                    context.Ingredients.Add(new Ingredient { Name = "Test" });
                }
                if (kind == "unique")
                    context.LoyaltyLevels.Add(new LoyaltyLevel { Name = "Basic" });
                if (kind == "fk")
                    context.Customers.Add(new Customer
                    {
                        FirstName = "Test", LastName = "Customer", LoyaltyCode = new string('B', 32), LoyaltyLevelId = 999
                    });
                if (kind == "check")
                    context.LoyaltyLevels.Add(new LoyaltyLevel { Name = "Invalid", MinOrders = -1 });
                if (kind is "unique" or "fk" or "check" or "fk_restrict" or "trigger")
                {
                    try { context.SaveChanges(); }
                    catch (DbUpdateException exception) when (kind is "fk_restrict" or "trigger")
                    {
                        Assert.That(((SqliteException)exception.InnerException!).SqliteExtendedErrorCode, Is.EqualTo(1811));
                        throw;
                    }
                }
                throw kind switch
                {
                    "busy" => new SqliteException("Test confidential data", 5),
                    "locked" => new SqliteException("Test confidential data", 6),
                    "other_sqlite" => new SqliteException("Test confidential data", 1),
                    "other_trigger_message" => new SqliteException("Test confidential data: FOREIGN KEY constraint failed", 19, 1811),
                    "other_db" => new DbUpdateException("Test confidential data", new InvalidOperationException("private")),
                    _ => new InvalidOperationException("Test confidential data")
                };
            });
        });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/ingredients");
        var body = await response.Content.ReadAsStringAsync();
        Assert.That((int)response.StatusCode, Is.EqualTo(status), body);
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));
        using var document = JsonDocument.Parse(body);
        Assert.That(document.RootElement.GetProperty("code").GetString(), Is.EqualTo(code));
        Assert.That(body, Does.Not.Contain("confidential").And.Not.Contain("SqliteException")
            .And.Not.Contain("stackTrace").And.Not.Contain("INSERT INTO").And.Not.Contain("Data Source=")
            .And.Not.Contain(new string('B', 32)));
        if (kind == "fk_restrict")
        {
            await factory.ReadAsync(async context =>
            {
                Assert.That(await context.Ingredients.CountAsync(), Is.EqualTo(1));
                Assert.That(await context.DishIngredients.CountAsync(), Is.EqualTo(1));
                Assert.That(await context.Dishes.CountAsync(), Is.EqualTo(1));
                return true;
            });
        }
        if (kind == "trigger")
            Assert.That(await factory.ReadAsync(context => context.Ingredients.CountAsync()), Is.Zero);
    }
}
