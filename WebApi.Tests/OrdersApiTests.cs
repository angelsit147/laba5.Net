using System.Net;
using System.Net.Http.Json;
using Business.Models;
using Data.Enums;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public class OrdersApiTests : ApiTestBase
{
    [Test]
    public async Task GuestDraft_CanEditTypeMergeChangeAndPhysicallyRemoveItems()
    {
        var catalog = await Catalog();
        using var response = await Client.PostAsJsonAsync("/api/orders", new { OrderType = "DineIn" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>(JsonOptions))!;
        Assert.That(response.Headers.Location!.OriginalString, Does.EndWith($"/api/orders/{order.Id}"));
        Assert.That(order.Status, Is.EqualTo(OrderStatus.Draft));
        Assert.That(order.CreatedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(order.DiscountPercentFixed, Is.Null);
        Assert.That(order.Items, Is.Empty);
        order = await Put<OrderDto>($"/api/orders/{order.Id}", new { OrderType = "Takeaway" });
        Assert.That(order.OrderType, Is.EqualTo(OrderType.Takeaway));
        await Post<OrderDto>($"/api/orders/{order.Id}/items", new { MenuItemId = catalog.First.Id, Quantity = 1 });
        order = await Post<OrderDto>($"/api/orders/{order.Id}/items", new { MenuItemId = catalog.First.Id, Quantity = 2 });
        Assert.That(order.Items.Single().Quantity, Is.EqualTo(3));
        order = await Post<OrderDto>($"/api/orders/{order.Id}/items", new { MenuItemId = catalog.Second.Id, Quantity = 2 });
        Assert.That(order.Total, Is.EqualTo(400m));
        var first = order.Items.Single(item => item.MenuItemId == catalog.First.Id);
        order = await Put<OrderDto>($"/api/orders/{order.Id}/items/{first.Id}", new { Quantity = 1 });
        Assert.That(order.Total, Is.EqualTo(200m));
        await Expect(HttpMethod.Delete, $"/api/orders/{order.Id}/items/{first.Id}", HttpStatusCode.NoContent);
        Assert.That(await Factory.ReadAsync(context => context.OrderItems.CountAsync()), Is.EqualTo(1));
        Assert.That((await Get<OrderDto>($"/api/orders/{order.Id}")).Total, Is.EqualTo(100m));
    }

    [Test]
    public async Task LoyaltyCode_AssignsCustomerAndNullRemovesCard()
    {
        var customer = await Register();
        var order = await Post<OrderDto>("/api/orders",
            new { OrderType = "Delivery", LoyaltyCode = $" {customer.LoyaltyCode.ToLowerInvariant()} " }, HttpStatusCode.Created);
        Assert.That(await Factory.ReadAsync(context => context.Orders.Where(value => value.Id == order.Id)
            .Select(value => value.CustomerId).SingleAsync()), Is.Not.Null);
        await Put<OrderDto>($"/api/orders/{order.Id}", new { OrderType = "Delivery", LoyaltyCode = (string?)null });
        Assert.That(await Factory.ReadAsync(context => context.Orders.Where(value => value.Id == order.Id)
            .Select(value => value.CustomerId).SingleAsync()), Is.Null);
    }

    [TestCase("", HttpStatusCode.BadRequest)]
    [TestCase("bad", HttpStatusCode.BadRequest)]
    [TestCase("00000000000000000000000000000000", HttpStatusCode.NotFound)]
    public async Task InvalidCard_DoesNotCreateGuest(string code, HttpStatusCode status)
    {
        await Expect(HttpMethod.Post, "/api/orders", status, new { OrderType = "DineIn", LoyaltyCode = code });
        Assert.That(await Factory.ReadAsync(context => context.Orders.CountAsync()), Is.Zero);
    }

    [Test]
    public async Task ForeignItem_CannotBeEditedOrDeleted()
    {
        var catalog = await Catalog();
        var first = await Draft(catalog.First.Id);
        var other = await Draft(catalog.Second.Id);
        var itemId = other.Items.Single().Id;
        await Expect(HttpMethod.Put, $"/api/orders/{first.Id}/items/{itemId}", HttpStatusCode.NotFound, new { Quantity = 2 });
        await Expect(HttpMethod.Delete, $"/api/orders/{first.Id}/items/{itemId}", HttpStatusCode.NotFound);
        Assert.That((await Get<OrderDto>($"/api/orders/{other.Id}")).Items.Single().Quantity, Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task InvalidQuantity_Is400(int quantity)
    {
        var catalog = await Catalog();
        var order = await Draft(catalog.First.Id);
        await Expect(HttpMethod.Post, $"/api/orders/{order.Id}/items", HttpStatusCode.BadRequest,
            new { MenuItemId = catalog.First.Id, Quantity = quantity });
        await Expect(HttpMethod.Put, $"/api/orders/{order.Id}/items/{order.Items[0].Id}", HttpStatusCode.BadRequest,
            new { Quantity = quantity });
    }

    [TestCase(OrderStatus.Draft)]
    [TestCase(OrderStatus.Confirmed)]
    [TestCase(OrderStatus.Completed)]
    [TestCase(OrderStatus.Cancelled)]
    public async Task TransitionMatrix_RejectsAllForbiddenAndRepeatedCommands(OrderStatus starting)
    {
        var catalog = await Catalog();
        foreach (var command in new[] { "confirm", "complete", "cancel" })
        {
            var order = await Draft(catalog.First.Id);
            if (starting is OrderStatus.Confirmed or OrderStatus.Completed)
                await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{order.Id}/confirm");
            if (starting == OrderStatus.Completed)
                await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{order.Id}/complete");
            if (starting == OrderStatus.Cancelled)
                await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{order.Id}/cancel");
            var allowed = starting == OrderStatus.Draft && command is "confirm" or "cancel"
                || starting == OrderStatus.Confirmed && command is "complete" or "cancel";
            await Expect(HttpMethod.Post, $"/api/orders/{order.Id}/{command}",
                allowed ? HttpStatusCode.OK : HttpStatusCode.Conflict);
            if (allowed)
                await Expect(HttpMethod.Post, $"/api/orders/{order.Id}/{command}", HttpStatusCode.Conflict);
        }
    }

    [TestCase("confirm")]
    [TestCase("complete")]
    [TestCase("cancel")]
    public async Task NonDraft_CannotEditAnyDraftField(string finalCommand)
    {
        var catalog = await Catalog();
        var order = await Draft(catalog.First.Id);
        if (finalCommand != "cancel") await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{order.Id}/confirm");
        if (finalCommand != "confirm") await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{order.Id}/{finalCommand}");
        await Expect(HttpMethod.Put, $"/api/orders/{order.Id}", HttpStatusCode.Conflict, new { OrderType = "Delivery" });
        await Expect(HttpMethod.Post, $"/api/orders/{order.Id}/items", HttpStatusCode.Conflict,
            new { MenuItemId = catalog.Second.Id, Quantity = 1 });
        await Expect(HttpMethod.Put, $"/api/orders/{order.Id}/items/{order.Items[0].Id}", HttpStatusCode.Conflict,
            new { Quantity = 2 });
        await Expect(HttpMethod.Delete, $"/api/orders/{order.Id}/items/{order.Items[0].Id}", HttpStatusCode.Conflict);
    }

    [Test]
    public async Task EmptyOrder_CannotBeConfirmed()
    {
        var order = await Post<OrderDto>("/api/orders", new { OrderType = "DineIn" }, HttpStatusCode.Created);
        await Expect(HttpMethod.Post, $"/api/orders/{order.Id}/confirm", HttpStatusCode.Conflict);
    }
}
