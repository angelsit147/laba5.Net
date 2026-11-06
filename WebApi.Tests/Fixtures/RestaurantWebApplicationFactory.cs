using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Business.Models;
using Data.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using WebApi.Services;

namespace WebApi.Tests.Fixtures;

public class RestaurantWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"restaurant-tests-{Guid.NewGuid():N}.db");
    private readonly Action<IServiceCollection>? configureServices;

    public RestaurantWebApplicationFactory(Action<IServiceCollection>? configureServices = null)
    {
        this.configureServices = configureServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:RestaurantDb"] = $"Data Source={databasePath};Foreign Keys=True;Pooling=False",
                ["LoyaltySeed:Silver:MinOrders"] = "5",
                ["LoyaltySeed:Silver:MinTotalSpent"] = "2000",
                ["LoyaltySeed:Silver:DiscountPercent"] = "5",
                ["LoyaltySeed:Gold:MinOrders"] = "10",
                ["LoyaltySeed:Gold:MinTotalSpent"] = "5000",
                ["LoyaltySeed:Gold:DiscountPercent"] = "10",
                ["Logging:LogLevel:Default"] = "Warning"
            }));
        if (configureServices is not null) builder.ConfigureTestServices(configureServices);
    }

    public async Task<T> ReadAsync<T>(Func<RestaurantDbContext, Task<T>> read)
    {
        await using var scope = Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<RestaurantDbContext>());
    }

    public async Task SeedAgainAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(databasePath + suffix);
        }
    }
}

public abstract class ApiTestBase
{
    protected RestaurantWebApplicationFactory Factory = null!;
    protected HttpClient Client = null!;
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    [SetUp]
    public void Start()
    {
        Factory = new RestaurantWebApplicationFactory();
        Client = Factory.CreateClient();
    }

    [TearDown]
    public void Stop()
    {
        Client.Dispose();
        Factory.Dispose();
    }

    protected async Task<T> Send<T>(HttpMethod method, string path, object? body = null,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await Client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(expected), content);
        return JsonSerializer.Deserialize<T>(content, JsonOptions)!;
    }

    protected Task<T> Get<T>(string path) => Send<T>(HttpMethod.Get, path);
    protected Task<T> Post<T>(string path, object body, HttpStatusCode expected = HttpStatusCode.OK) =>
        Send<T>(HttpMethod.Post, path, body, expected);
    protected Task<T> Put<T>(string path, object body) => Send<T>(HttpMethod.Put, path, body);

    protected async Task Expect(HttpMethod method, string path, HttpStatusCode status, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await Client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(status), await response.Content.ReadAsStringAsync());
        if (status == HttpStatusCode.NoContent)
            Assert.That(await response.Content.ReadAsByteArrayAsync(), Is.Empty);
    }

    protected async Task<(DishDto Dish, MenuItemDto First, MenuItemDto Second)> Catalog(decimal firstPrice = 100m)
    {
        var ingredient = await Post<IngredientDto>("/api/ingredients", new { Name = "Test ingredient" }, HttpStatusCode.Created);
        var dish = await Post<DishDto>("/api/dishes", new
        {
            Name = "Test dish", IsAvailable = true, IngredientIds = new[] { ingredient.Id }
        }, HttpStatusCode.Created);
        var first = await Post<MenuItemDto>("/api/menu", MenuWrite(dish.Id, firstPrice), HttpStatusCode.Created);
        var second = await Post<MenuItemDto>("/api/menu", MenuWrite(dish.Id, 50m, "Small"), HttpStatusCode.Created);
        return (dish, first, second);
    }

    protected static object MenuWrite(int dishId, decimal price, string name = "Regular", bool isAvailable = true) =>
        new { DishId = dishId, Price = price, PortionName = name, PortionDescription = "Test portion", IsAvailable = isAvailable };

    protected async Task<OrderDto> Draft(int menuId, string? code = null, int quantity = 1)
    {
        var order = await Post<OrderDto>("/api/orders", new { OrderType = "DineIn", LoyaltyCode = code }, HttpStatusCode.Created);
        return await Post<OrderDto>($"/api/orders/{order.Id}/items", new { MenuItemId = menuId, Quantity = quantity });
    }

    protected async Task<OrderDto> Finish(int menuId, string? code = null)
    {
        var order = await Draft(menuId, code);
        await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{order.Id}/confirm");
        return await Send<OrderDto>(HttpMethod.Post, $"/api/orders/{order.Id}/complete");
    }

    protected Task<CustomerRegistrationResultDto> Register() =>
        Post<CustomerRegistrationResultDto>("/api/customers", new { FirstName = "Test", LastName = "Customer" }, HttpStatusCode.Created);

    protected Task<LoyaltyCardDto> Card(string code) =>
        Post<LoyaltyCardDto>("/api/customers/resolve-loyalty", new { LoyaltyCode = code });
}
