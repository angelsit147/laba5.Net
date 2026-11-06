using System.Net;
using System.Net.Http.Json;
using Business.Models;
using NUnit.Framework;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public class MenuApiTests : ApiTestBase
{
    [Test]
    public async Task PriceList_UsesMenuDishAndCompositionAvailability()
    {
        var catalog = await Catalog(123.45m);
        using var created = await Client.PostAsJsonAsync("/api/menu", MenuWrite(catalog.Dish.Id, 10m, "Hidden", false));
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var hidden = (await created.Content.ReadFromJsonAsync<MenuItemDto>())!;
        Assert.That(created.Headers.Location!.OriginalString, Does.EndWith($"/api/menu/{hidden.Id}"));
        Assert.That((await Get<MenuItemDto>(created.Headers.Location.OriginalString)).Price, Is.EqualTo(10m));
        Assert.That((await Get<MenuItemDto[]>("/api/menu?availableOnly=true")).Select(value => value.Id),
            Is.EquivalentTo(new[] { catalog.First.Id, catalog.Second.Id }));
        Assert.That(await Get<MenuItemDto[]>("/api/menu?availableOnly=false"), Has.Length.EqualTo(3));
        Assert.That((await Get<MenuItemDto>($"/api/menu/{catalog.First.Id}")).Price, Is.EqualTo(123.45m));
        await Put<DishDto>($"/api/dishes/{catalog.Dish.Id}",
            new { Name = "Hidden dish", IsAvailable = false, IngredientIds = Array.Empty<int>() });
        Assert.That(await Get<MenuItemDto[]>("/api/menu?availableOnly=true"), Is.Empty);
        Assert.That(await Get<MenuItemDto[]>("/api/menu?availableOnly=false"), Has.Length.EqualTo(3));
        await Expect(HttpMethod.Delete, $"/api/menu/{hidden.Id}", HttpStatusCode.NoContent);
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("0.001")]
    [TestCase("92233720368547758.08")]
    public async Task InvalidPrice_Is400(string price)
    {
        var catalog = await Catalog();
        await Expect(HttpMethod.Post, "/api/menu", HttpStatusCode.BadRequest,
            MenuWrite(catalog.Dish.Id, decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Test]
    public async Task DishIdIsImmutableAndReferencedPortionCannotBeDeleted()
    {
        var catalog = await Catalog();
        var other = await Post<DishDto>("/api/dishes",
            new { Name = "Other", IsAvailable = false, IngredientIds = Array.Empty<int>() }, HttpStatusCode.Created);
        await Expect(HttpMethod.Put, $"/api/menu/{catalog.First.Id}", HttpStatusCode.Conflict, MenuWrite(other.Id, 100m));
        await Draft(catalog.First.Id);
        await Expect(HttpMethod.Delete, $"/api/menu/{catalog.First.Id}", HttpStatusCode.Conflict);
        Assert.That((await Get<MenuItemDto>($"/api/menu/{catalog.First.Id}")).DishId, Is.EqualTo(catalog.Dish.Id));
    }
}
