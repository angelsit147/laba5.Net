using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Business.Models;
using NUnit.Framework;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public class CustomersApiTests : ApiTestBase
{
    [Test]
    public async Task RegistrationAndResolve_ReturnOnlyApprovedCardData()
    {
        using var response = await Client.PostAsJsonAsync("/api/customers",
            new { FirstName = "Test", LastName = "Customer", PhoneNumber = "0000000000" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Null);
        var result = (await response.Content.ReadFromJsonAsync<CustomerRegistrationResultDto>())!;
        Assert.That(result.LevelName, Is.EqualTo("Basic"));
        Assert.That(result.DiscountPercent, Is.Zero);
        Assert.That(result.LoyaltyCode, Does.Match("^[0-9A-F]{32}$"));
        var card = await Post<JsonElement>("/api/customers/resolve-loyalty",
            new { LoyaltyCode = $"  {result.LoyaltyCode.ToLowerInvariant()}  " });
        Assert.That(card.EnumerateObject().Select(property => property.Name),
            Is.EquivalentTo(new[] { "levelName", "discountPercent" }));
    }

    [TestCase("", HttpStatusCode.BadRequest)]
    [TestCase("not-a-card", HttpStatusCode.BadRequest)]
    [TestCase("00000000000000000000000000000000", HttpStatusCode.NotFound)]
    public Task InvalidOrUnknownCode_ReturnsExpectedStatus(string code, HttpStatusCode status) =>
        Expect(HttpMethod.Post, "/api/customers/resolve-loyalty", status, new { LoyaltyCode = code });

    [TestCase("GET", "/api/customers", HttpStatusCode.MethodNotAllowed)]
    [TestCase("GET", "/api/customers/1", HttpStatusCode.NotFound)]
    [TestCase("PUT", "/api/customers/1", HttpStatusCode.NotFound)]
    [TestCase("DELETE", "/api/customers/1", HttpStatusCode.NotFound)]
    [TestCase("GET", "/api/customers/1/orders", HttpStatusCode.NotFound)]
    public Task UnapprovedRoutes_AreAbsent(string method, string path, HttpStatusCode status) =>
        Expect(new HttpMethod(method), path, status);
}
