using System.Net;
using System.Net.Http.Json;
using Business.Models;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public class DishesApiTests : ApiTestBase
{
    [Test]
    public async Task EmptyUnavailableDish_CanBeCreatedUpdatedAndDeleted()
    {
        Assert.That(await Get<DishDto[]>("/api/dishes"), Is.Empty);
        using var created = await Client.PostAsJsonAsync("/api/dishes",
            new { Name = "Unfinished", IsAvailable = false, IngredientIds = Array.Empty<int>() });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var dish = (await created.Content.ReadFromJsonAsync<DishDto>())!;
        Assert.That(created.Headers.Location!.OriginalString, Does.EndWith($"/api/dishes/{dish.Id}"));
        Assert.That(dish.IsAvailable, Is.False);
        Assert.That((await Get<DishDto>(created.Headers.Location.OriginalString)).Ingredients, Is.Empty);
        var ingredient = await Post<IngredientDto>("/api/ingredients", new { Name = "Rice" }, HttpStatusCode.Created);
        dish = await Put<DishDto>($"/api/dishes/{dish.Id}",
            new { Name = "Ready", IsAvailable = true, IngredientIds = new[] { ingredient.Id } });
        Assert.That(dish.Ingredients.Single().Id, Is.EqualTo(ingredient.Id));
        dish = await Put<DishDto>($"/api/dishes/{dish.Id}",
            new { Name = "Unfinished again", IsAvailable = false, IngredientIds = Array.Empty<int>() });
        Assert.That(dish.Ingredients, Is.Empty);
        await Expect(HttpMethod.Delete, $"/api/dishes/{dish.Id}", HttpStatusCode.NoContent);
        Assert.That(await Factory.ReadAsync(context => context.DishIngredients.CountAsync()), Is.Zero);
        Assert.That(await Factory.ReadAsync(context => context.Ingredients.CountAsync()), Is.EqualTo(1));
    }

    [Test]
    public async Task AvailableDish_RequiresIngredientsAndFailedPutPreservesGraph()
    {
        await Expect(HttpMethod.Post, "/api/dishes", HttpStatusCode.BadRequest,
            new { Name = "Invalid", IsAvailable = true, IngredientIds = Array.Empty<int>() });
        var catalog = await Catalog();
        await Expect(HttpMethod.Put, $"/api/dishes/{catalog.Dish.Id}", HttpStatusCode.BadRequest,
            new { Name = "Should not change", IsAvailable = true, IngredientIds = Array.Empty<int>() });
        var current = await Get<DishDto>($"/api/dishes/{catalog.Dish.Id}");
        Assert.That(current.Name, Is.EqualTo(catalog.Dish.Name));
        Assert.That(current.Ingredients, Has.Count.EqualTo(1));
        await Expect(HttpMethod.Delete, $"/api/dishes/{current.Id}", HttpStatusCode.Conflict);
    }

    [TestCase("duplicate", HttpStatusCode.BadRequest)]
    [TestCase("unknown", HttpStatusCode.NotFound)]
    [TestCase("negative", HttpStatusCode.BadRequest)]
    public async Task InvalidComposition_DoesNotPartiallyUpdate(string kind, HttpStatusCode status)
    {
        var catalog = await Catalog();
        var originalId = catalog.Dish.Ingredients[0].Id;
        var ids = kind == "duplicate" ? new[] { originalId, originalId }
            : new[] { originalId, kind == "unknown" ? 999 : -1 };
        await Expect(HttpMethod.Put, $"/api/dishes/{catalog.Dish.Id}", status,
            new { Name = "Changed", IsAvailable = true, IngredientIds = ids });
        var current = await Get<DishDto>($"/api/dishes/{catalog.Dish.Id}");
        Assert.That(current.Name, Is.EqualTo(catalog.Dish.Name));
        Assert.That(current.Ingredients.Select(value => value.Id), Is.EqualTo(new[] { originalId }));
    }

    [Test]
    public async Task ReplacingComposition_DeletesOldLinkAndRetainsIngredient()
    {
        var catalog = await Catalog();
        var second = await Post<IngredientDto>("/api/ingredients", new { Name = "Other" }, HttpStatusCode.Created);
        await Put<DishDto>($"/api/dishes/{catalog.Dish.Id}",
            new { Name = "Changed", IsAvailable = true, IngredientIds = new[] { second.Id } });
        var ids = await Factory.ReadAsync(context => context.DishIngredients.Select(link => link.IngredientId).ToListAsync());
        Assert.That(ids, Is.EqualTo(new[] { second.Id }));
        Assert.That(await Factory.ReadAsync(context => context.Ingredients.CountAsync()), Is.EqualTo(2));
    }
}
