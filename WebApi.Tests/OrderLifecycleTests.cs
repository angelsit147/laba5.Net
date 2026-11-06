using Business.Models;
using Data.Enums;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public class OrderLifecycleTests : ApiTestBase
{
    [Test]
    public async Task FullHttpLifecycle_UsesCurrentQuoteFixedHistoryAndRealSilverGoldThresholds()
    {
        var customer = await Register();
        Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo("Basic"));
        var catalog = await Catalog();
        var draft = await Draft(catalog.First.Id, customer.LoyaltyCode, 2);
        draft = await Post<OrderDto>($"/api/orders/{draft.Id}/items", new { MenuItemId = catalog.Second.Id, Quantity = 1 });
        Assert.That(draft.Total, Is.EqualTo(250m));
        var savedBefore = await Factory.ReadAsync(context => context.OrderItems.AsNoTracking()
            .Where(item => item.OrderId == draft.Id).OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.UnitPrice, item.DishNameFixed, item.PortionNameFixed, item.PortionDescriptionFixed }).ToListAsync());
        await Put<MenuItemDto>($"/api/menu/{catalog.First.Id}", MenuWrite(catalog.Dish.Id, 150m, "New name"));
        draft = await Get<OrderDto>($"/api/orders/{draft.Id}");
        Assert.That(draft.Total, Is.EqualTo(350m));
        Assert.That(draft.IsEstimate, Is.True);
        var savedAfter = await Factory.ReadAsync(context => context.OrderItems.AsNoTracking()
            .Where(item => item.OrderId == draft.Id).OrderBy(item => item.Id)
            .Select(item => new { item.Id, item.UnitPrice, item.DishNameFixed, item.PortionNameFixed, item.PortionDescriptionFixed }).ToListAsync());
        Assert.That(savedAfter, Is.EqualTo(savedBefore));
        var confirmed = await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{draft.Id}/confirm");
        Assert.That(confirmed.Items.Single(item => item.MenuItemId == catalog.First.Id).PortionName, Is.EqualTo("New name"));
        Assert.That(confirmed.DiscountPercentFixed, Is.Zero);
        Assert.That(confirmed.IsEstimate, Is.False);
        await Put<MenuItemDto>($"/api/menu/{catalog.First.Id}", MenuWrite(catalog.Dish.Id, 999m));
        Assert.That((await Get<OrderDto>($"/api/orders/{draft.Id}")).Total, Is.EqualTo(350m));
        await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{draft.Id}/complete");
        var heldDraft = await Draft(catalog.Second.Id, customer.LoyaltyCode);
        for (var count = 2; count <= 5; count++)
        {
            await Put<MenuItemDto>($"/api/menu/{catalog.First.Id}", MenuWrite(catalog.Dish.Id, count == 5 ? 450m : 400m));
            await Finish(catalog.First.Id, customer.LoyaltyCode);
            Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo(count < 5 ? "Basic" : "Silver"));
        }

        var currentEstimate = await Get<OrderDto>($"/api/orders/{heldDraft.Id}");
        Assert.That(currentEstimate.AppliedDiscountPercent, Is.EqualTo(5m));
        Assert.That(currentEstimate.DiscountPercentFixed, Is.Null);
        Assert.That(await Factory.ReadAsync(context => context.Orders.Where(order => order.Id == heldDraft.Id)
            .Select(order => order.DiscountPercentFixed).SingleAsync()), Is.Null);
        await Put<MenuItemDto>($"/api/menu/{catalog.First.Id}", MenuWrite(catalog.Dish.Id, 400m));
        for (var count = 6; count <= 10; count++)
        {
            var completed = await Finish(catalog.First.Id, customer.LoyaltyCode);
            Assert.That(completed.DiscountPercentFixed, Is.EqualTo(5m));
            Assert.That(completed.Total, Is.EqualTo(380m));
            Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo("Silver"));
        }

        await Put<MenuItemDto>($"/api/menu/{catalog.First.Id}", MenuWrite(catalog.Dish.Id, 1157.89m));
        var reachesGold = await Finish(catalog.First.Id, customer.LoyaltyCode);
        Assert.That(reachesGold.Total, Is.EqualTo(1100m));
        Assert.That(reachesGold.DiscountPercentFixed, Is.EqualTo(5m));
        Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo("Gold"));
        var next = await Draft(catalog.Second.Id, customer.LoyaltyCode);
        next = await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{next.Id}/confirm");
        Assert.That(next.DiscountPercentFixed, Is.EqualTo(10m));
        Assert.That(next.Total, Is.EqualTo(45m));
        Assert.That((await Get<OrderDto>($"/api/orders/{draft.Id}")).Total, Is.EqualTo(350m));
        var net = await Factory.ReadAsync(async context =>
        {
            var orders = await context.Orders.Include(order => order.Items).Where(order => order.Status == OrderStatus.Completed).ToListAsync();
            return orders.Sum(Business.Pricing.OrderPricing.HistoricalTotal);
        });
        Assert.That(net, Is.EqualTo(5000m));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Silver_RequiresBothCountAndSpent(bool spentFirst)
    {
        var catalog = await Catalog(spentFirst ? 2000m : 100m);
        var customer = await Register();
        if (spentFirst)
        {
            await Finish(catalog.First.Id, customer.LoyaltyCode);
            Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo("Basic"));
            await Put<MenuItemDto>($"/api/menu/{catalog.First.Id}", MenuWrite(catalog.Dish.Id, 0.01m));
            for (var count = 2; count <= 5; count++)
            {
                await Finish(catalog.First.Id, customer.LoyaltyCode);
                Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo(count == 5 ? "Silver" : "Basic"));
            }
        }
        else
        {
            for (var count = 1; count <= 5; count++) await Finish(catalog.First.Id, customer.LoyaltyCode);
            Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo("Basic"));
            await Put<MenuItemDto>($"/api/menu/{catalog.First.Id}", MenuWrite(catalog.Dish.Id, 1500m));
            await Finish(catalog.First.Id, customer.LoyaltyCode);
            Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo("Silver"));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Gold_UsesNetSpendingAndInclusiveCountThreshold(bool spentFirst)
    {
        var catalog = await Catalog(400m);
        var customer = await Register();
        for (var count = 1; count <= 5; count++) await Finish(catalog.First.Id, customer.LoyaltyCode);
        for (var count = 6; count <= 10; count++)
        {

            var price = spentFirst ? (count == 6 ? 3157.89m : 0.01m) : (count == 10 ? 1052.63m : 526.32m);
            await Put<MenuItemDto>($"/api/menu/{catalog.First.Id}", MenuWrite(catalog.Dish.Id, price));
            var completed = await Finish(catalog.First.Id, customer.LoyaltyCode);
            Assert.That(completed.DiscountPercentFixed, Is.EqualTo(5m));
            Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo(count == 10 ? "Gold" : "Silver"));
        }
        if (!spentFirst)
        {
            var history = await Factory.ReadAsync(context => context.Orders.Include(order => order.Items).ToListAsync());
            Assert.That(history.Sum(Business.Pricing.OrderPricing.HistoricalTotal), Is.EqualTo(5000m));
            Assert.That(history.Count, Is.EqualTo(10));
        }
        var next = await Draft(catalog.First.Id, customer.LoyaltyCode);
        Assert.That((await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{next.Id}/confirm")).DiscountPercentFixed, Is.EqualTo(10m));
    }

    [Test]
    public async Task GuestsAndCancelledOrders_DoNotContributeToCustomerStatistics()
    {
        var catalog = await Catalog(2500m);
        var customer = await Register();
        for (var index = 0; index < 5; index++)
        {
            await Finish(catalog.First.Id);
            var cancelled = await Draft(catalog.First.Id, customer.LoyaltyCode);
            if (index % 2 == 0) await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{cancelled.Id}/confirm");
            await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{cancelled.Id}/cancel");
        }
        Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo("Basic"));
        await Finish(catalog.First.Id, customer.LoyaltyCode);
        Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo("Basic"));
        Assert.That(await Factory.ReadAsync(context => context.Orders.CountAsync(order =>
            order.CustomerId != null && order.Status == OrderStatus.Completed)), Is.EqualTo(1));
    }
}
