using System.Text.Json;
using NUnit.Framework;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public class OpenApiContractTests : ApiTestBase
{
    [Test]
    public async Task Swagger_DescribesAllRoutesStatusesAndResponseDtos()
    {
        (string Path, string Method, int Success, string Dto, bool Array, int[] Errors)[] operations =
        [
            ("/api/ingredients", "get", 200, "IngredientDto", true, Array.Empty<int>()),
            ("/api/ingredients", "post", 201, "IngredientDto", false, new[] { 400 }),
            ("/api/ingredients/{id}", "get", 200, "IngredientDto", false, new[] { 400, 404 }),
            ("/api/ingredients/{id}", "put", 200, "IngredientDto", false, new[] { 400, 404 }),
            ("/api/ingredients/{id}", "delete", 204, "", false, new[] { 400, 404, 409 }),
            ("/api/dishes", "get", 200, "DishDto", true, Array.Empty<int>()),
            ("/api/dishes", "post", 201, "DishDto", false, new[] { 400, 404 }),
            ("/api/dishes/{id}", "get", 200, "DishDto", false, new[] { 400, 404 }),
            ("/api/dishes/{id}", "put", 200, "DishDto", false, new[] { 400, 404 }),
            ("/api/dishes/{id}", "delete", 204, "", false, new[] { 400, 404, 409 }),
            ("/api/menu", "get", 200, "MenuItemDto", true, new[] { 400 }),
            ("/api/menu", "post", 201, "MenuItemDto", false, new[] { 400, 404 }),
            ("/api/menu/{id}", "get", 200, "MenuItemDto", false, new[] { 400, 404 }),
            ("/api/menu/{id}", "put", 200, "MenuItemDto", false, new[] { 400, 404, 409 }),
            ("/api/menu/{id}", "delete", 204, "", false, new[] { 400, 404, 409 }),
            ("/api/customers", "post", 201, "CustomerRegistrationResultDto", false, new[] { 400, 409 }),
            ("/api/customers/resolve-loyalty", "post", 200, "LoyaltyCardDto", false, new[] { 400, 404 }),
            ("/api/loyalty-levels", "get", 200, "LoyaltyLevelDto", true, Array.Empty<int>()),
            ("/api/loyalty-levels/{id}", "get", 200, "LoyaltyLevelDto", false, new[] { 400, 404 }),
            ("/api/loyalty-levels", "put", 200, "LoyaltyLevelDto", true, new[] { 400, 404 }),
            ("/api/orders", "post", 201, "OrderDto", false, new[] { 400, 404, 409 }),
            ("/api/orders/{id}", "get", 200, "OrderDto", false, new[] { 400, 404, 409 }),
            ("/api/orders/{id}", "put", 200, "OrderDto", false, new[] { 400, 404, 409 }),
            ("/api/orders/{id}/items", "post", 200, "OrderDto", false, new[] { 400, 404, 409 }),
            ("/api/orders/{id}/items/{itemId}", "put", 200, "OrderDto", false, new[] { 400, 404, 409 }),
            ("/api/orders/{id}/items/{itemId}", "delete", 204, "", false, new[] { 400, 404, 409 }),
            ("/api/orders/{id}/confirm", "post", 200, "OrderDto", false, new[] { 400, 404, 409 }),
            ("/api/orders/{id}/complete", "post", 200, "OrderDto", false, new[] { 400, 404, 409 }),
            ("/api/orders/{id}/cancel", "post", 200, "OrderDto", false, new[] { 400, 404, 409 })
        ];
        var document = await Get<JsonElement>("/swagger/v1/swagger.json");
        var paths = document.GetProperty("paths");
        var actual = paths.EnumerateObject().SelectMany(path => path.Value.EnumerateObject()
            .Select(method => (Path: path.Name, Method: method.Name)));
        Assert.That(actual, Is.EquivalentTo(operations.Select(operation => (operation.Path, operation.Method))));
        foreach (var operation in operations)
        {
            var responses = document.GetProperty("paths").GetProperty(operation.Path)
                .GetProperty(operation.Method).GetProperty("responses");
            Assert.That(responses.EnumerateObject().Select(value => value.Name),
                Is.EquivalentTo(operation.Errors.Select(value => value.ToString()).Append(operation.Success.ToString())));
            var success = responses.GetProperty(operation.Success.ToString());
            if (operation.Success == 204)
            {
                Assert.That(success.TryGetProperty("content", out _), Is.False);
            }
            else
            {
                var schema = success.GetProperty("content").GetProperty("application/json").GetProperty("schema");
                if (operation.Array)
                {
                    Assert.That(schema.GetProperty("type").GetString(), Is.EqualTo("array"));
                    schema = schema.GetProperty("items");
                }
                Assert.That(schema.GetProperty("$ref").GetString(), Is.EqualTo("#/components/schemas/" + operation.Dto));
            }
            foreach (var code in operation.Errors)
            {
                var content = responses.GetProperty(code.ToString()).GetProperty("content");
                Assert.That(content.EnumerateObject().Select(value => value.Name),
                    Is.EqualTo(new[] { "application/problem+json" }));
                Assert.That(content.GetProperty("application/problem+json").GetProperty("schema")
                    .GetProperty("$ref").GetString(), Is.EqualTo("#/components/schemas/ProblemDetails"));
            }
        }
    }
}
