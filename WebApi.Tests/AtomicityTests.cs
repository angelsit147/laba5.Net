using System.Net;
using Business.Models;
using Data.Enums;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public class AtomicityTests : ApiTestBase
{
    [TestCase("menu")]
    [TestCase("dish")]
    [TestCase("composition")]
    public async Task UnavailableSecondPortion_LeavesAllDraftSnapshotsAndStatusUnchanged(string reason)
    {
        var catalog = await Catalog();
        var order = await Draft(catalog.First.Id);
        order = await Post<OrderDto>($"/api/orders/{order.Id}/items", new { MenuItemId = catalog.Second.Id, Quantity = 1 });
        await Put<MenuItemDto>($"/api/menu/{catalog.First.Id}", MenuWrite(catalog.Dish.Id, 200m, "Changed"));
        if (reason == "menu")
            await Put<MenuItemDto>($"/api/menu/{catalog.Second.Id}", MenuWrite(catalog.Dish.Id, 50m, "Small", false));
        else
            await Put<DishDto>($"/api/dishes/{catalog.Dish.Id}", new
            {
                Name = catalog.Dish.Name, IsAvailable = false,
                IngredientIds = reason == "composition" ? Array.Empty<int>() : catalog.Dish.Ingredients.Select(item => item.Id).ToArray()
            });
        var quote = await Get<OrderDto>($"/api/orders/{order.Id}");
        Assert.That(quote.Items.Single(item => item.MenuItemId == catalog.Second.Id).IsAvailable, Is.False);
        await Expect(HttpMethod.Post, $"/api/orders/{order.Id}/confirm", HttpStatusCode.Conflict);
        await Factory.ReadAsync(async context =>
        {
            var saved = await context.Orders.Include(value => value.Items).SingleAsync();
            Assert.That(saved.Status, Is.EqualTo(OrderStatus.Draft));
            Assert.That(saved.DiscountPercentFixed, Is.Null);
            Assert.That(saved.Items.Single(item => item.MenuItemId == catalog.First.Id).UnitPrice, Is.EqualTo(100m));
            Assert.That(saved.Items.Single(item => item.MenuItemId == catalog.First.Id).PortionNameFixed, Is.EqualTo("Regular"));
            return true;
        });
    }

    [Test]
    public async Task FailedCompletion_DoesNotChangeOrderOrCustomer()
    {
        var customer = await Register();
        var catalog = await Catalog();
        var order = await Draft(catalog.First.Id, customer.LoyaltyCode);
        await Expect(HttpMethod.Post, $"/api/orders/{order.Id}/complete", HttpStatusCode.Conflict);
        await Factory.ReadAsync(async context =>
        {
            Assert.That((await context.Orders.SingleAsync()).Status, Is.EqualTo(OrderStatus.Draft));
            Assert.That((await context.Customers.Include(value => value.LoyaltyLevel).SingleAsync()).LoyaltyLevel.Name, Is.EqualTo("Basic"));
            return true;
        });
    }

    [Test]
    public async Task CancelledDraftAndConfirmed_KeepTheirRespectiveSnapshotSemantics()
    {
        var catalog = await Catalog();
        foreach (var confirm in new[] { false, true })
        {
            var order = await Draft(catalog.First.Id);
            if (confirm) await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{order.Id}/confirm");
            await Put<MenuItemDto>($"/api/menu/{catalog.First.Id}", MenuWrite(catalog.Dish.Id, 200m));
            var cancelled = await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{order.Id}/cancel");
            Assert.That(cancelled.IsEstimate, Is.EqualTo(!confirm));
            Assert.That(cancelled.Items.Single().UnitPrice, Is.EqualTo(confirm ? 200m : 100m));
            Assert.That(cancelled.DiscountPercentFixed, Is.EqualTo(confirm ? 0m : (decimal?)null));
        }
    }
}
