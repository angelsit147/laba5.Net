using System.Net;
using System.Net.Http.Json;
using Business.Models;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public class IngredientsApiTests : ApiTestBase
{
    [Test]
    public async Task Crud_ReturnsExpectedStatusLocationAndPersistentValues()
    {
        const string name = "O'Neil \"&\"; DROP TABLE Ingredients; --";
        Assert.That(await Get<IngredientDto[]>("/api/ingredients"), Is.Empty);
        using var created = await Client.PostAsJsonAsync("/api/ingredients",
            new { Name = new string(' ', 120) + name + new string(' ', 120) });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var item = (await created.Content.ReadFromJsonAsync<IngredientDto>())!;
        Assert.That(created.Headers.Location!.OriginalString, Does.EndWith($"/api/ingredients/{item.Id}"));
        Assert.That((await Get<IngredientDto>(created.Headers.Location.OriginalString)).Name, Is.EqualTo(name));
        Assert.That((await Put<IngredientDto>($"/api/ingredients/{item.Id}", new { Name = "Pepper" })).Name, Is.EqualTo("Pepper"));
        await Expect(HttpMethod.Delete, $"/api/ingredients/{item.Id}", HttpStatusCode.NoContent);
        Assert.That(await Factory.ReadAsync(context => context.Ingredients.CountAsync()), Is.Zero);
    }

    [TestCase("GET")]
    [TestCase("PUT")]
    [TestCase("DELETE")]
    public Task UnknownId_Returns404(string method) =>
        Expect(new HttpMethod(method), "/api/ingredients/999", HttpStatusCode.NotFound,
            method == "PUT" ? new { Name = "Test" } : null);

    [TestCase("")]
    [TestCase("   ")]
    public async Task InvalidName_Returns400WithoutInsert(string name)
    {
        await Expect(HttpMethod.Post, "/api/ingredients", HttpStatusCode.BadRequest, new { Name = name });
        Assert.That(await Factory.ReadAsync(context => context.Ingredients.CountAsync()), Is.Zero);
    }

    [Test]
    public async Task UsedIngredient_CannotBeDeleted()
    {
        var catalog = await Catalog();
        await Expect(HttpMethod.Delete, $"/api/ingredients/{catalog.Dish.Ingredients[0].Id}", HttpStatusCode.Conflict);
        Assert.That(await Factory.ReadAsync(context => context.Ingredients.CountAsync()), Is.EqualTo(1));
    }
}
