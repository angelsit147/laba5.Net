using System.Net;
using Business.Models;
using Data.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using WebApi.Configuration;
using WebApi.Services;
using WebApi.Tests.Fixtures;

namespace WebApi.Tests;

public class LoyaltyLevelsApiTests : ApiTestBase
{
    [Test]
    public async Task Seed_UsesApprovedValuesAndRealGeneratedIdentities()
    {
        var levels = await Get<LoyaltyLevelDto[]>("/api/loyalty-levels");
        Assert.That(levels.Select(level => (level.Name, level.MinOrders, level.MinTotalSpent, level.DiscountPercent)),
            Is.EqualTo(new[] { ("Basic", 0, 0m, 0m), ("Silver", 5, 2000m, 5m), ("Gold", 10, 5000m, 10m) }));
        Assert.That((await Get<LoyaltyLevelDto>($"/api/loyalty-levels/{levels[1].Id}")).Name, Is.EqualTo("Silver"));
        await Factory.SeedAgainAsync();
        Assert.That(await Factory.ReadAsync(context => context.LoyaltyLevels.CountAsync()), Is.EqualTo(3));
        await Factory.ReadAsync(async context =>
        {
            context.LoyaltyLevels.RemoveRange(await context.LoyaltyLevels.ToListAsync());
            await context.SaveChangesAsync();
            return true;
        });
        await Factory.SeedAgainAsync();
        Assert.That((await Get<LoyaltyLevelDto[]>("/api/loyalty-levels")).All(level => level.Id > levels.Max(old => old.Id)), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PolicyUpdate_RecalculatesCustomersBothDirectionsAndPreservesHistory(bool promote)
    {
        var catalog = await Catalog(400m);
        var customer = await Register();
        var withoutHistory = await Register();
        var first = await Finish(catalog.First.Id, customer.LoyaltyCode);
        for (var index = 1; index < 6; index++) await Finish(catalog.First.Id, customer.LoyaltyCode);
        Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo("Silver"));
        var updates = await Get<LoyaltyLevelDto[]>("/api/loyalty-levels");
        updates.Single(level => level.Name == "Silver").MinOrders = promote ? 1 : 7;
        updates.Single(level => level.Name == "Silver").MinTotalSpent = promote ? 100m : 3000m;
        updates.Single(level => level.Name == "Gold").MinOrders = promote ? 2 : 10;
        updates.Single(level => level.Name == "Gold").MinTotalSpent = promote ? 300m : 5000m;
        await Put<LoyaltyLevelDto[]>("/api/loyalty-levels", Write(updates));
        Assert.That((await Card(customer.LoyaltyCode)).LevelName, Is.EqualTo(promote ? "Gold" : "Basic"));
        Assert.That((await Card(withoutHistory.LoyaltyCode)).LevelName, Is.EqualTo("Basic"));
        var history = await Get<OrderDto>($"/api/orders/{first.Id}");
        Assert.That(history.Total, Is.EqualTo(first.Total));
        Assert.That(history.DiscountPercentFixed, Is.EqualTo(first.DiscountPercentFixed));
        await Factory.SeedAgainAsync();
        var after = await Get<LoyaltyLevelDto[]>("/api/loyalty-levels");
        Assert.That(after.Single(level => level.Name == "Silver").MinOrders, Is.EqualTo(promote ? 1 : 7));
    }

    [TestCase("missing", HttpStatusCode.BadRequest)]
    [TestCase("duplicate", HttpStatusCode.BadRequest)]
    [TestCase("unknown", HttpStatusCode.NotFound)]
    [TestCase("basic", HttpStatusCode.BadRequest)]
    [TestCase("crossed", HttpStatusCode.BadRequest)]
    public async Task InvalidBatch_DoesNotChangeAnyLevel(string kind, HttpStatusCode status)
    {
        var original = await Get<LoyaltyLevelDto[]>("/api/loyalty-levels");
        var update = original.Select(level => new UpdateLoyaltyLevelDto
        {
            Id = level.Id, DiscountPercent = level.DiscountPercent, MinOrders = level.MinOrders, MinTotalSpent = level.MinTotalSpent
        }).ToList();
        switch (kind)
        {
            case "missing": update.RemoveAt(2); break;
            case "duplicate": update[2].Id = update[1].Id; break;
            case "unknown": update[2].Id = 999; break;
            case "basic": update[0].DiscountPercent = 1; break;
            case "crossed": update[1].MinTotalSpent = 6000; break;
        }
        await Expect(HttpMethod.Put, "/api/loyalty-levels", status, new UpdateLoyaltyLevelsDto { Levels = update });
        Assert.That((await Get<LoyaltyLevelDto[]>("/api/loyalty-levels")).Select(level =>
            (level.Id, level.DiscountPercent, level.MinOrders, level.MinTotalSpent)), Is.EqualTo(original.Select(level =>
            (level.Id, level.DiscountPercent, level.MinOrders, level.MinTotalSpent))));
    }

    [TestCase("missing")]
    [TestCase("missing_field")]
    [TestCase("negative")]
    [TestCase("crossed")]
    [TestCase("precision")]
    public async Task InvalidSeedConfiguration_FailsWithoutPartialSeed(string kind)
    {
        await Factory.ReadAsync(async context =>
        {
            context.LoyaltyLevels.RemoveRange(await context.LoyaltyLevels.ToListAsync());
            await context.SaveChangesAsync();
            var options = new LoyaltySeedOptions
            {
                Silver = new() { MinOrders = 5, MinTotalSpent = 2000m, DiscountPercent = 5m },
                Gold = new() { MinOrders = 10, MinTotalSpent = 5000m, DiscountPercent = 10m }
            };
            if (kind == "missing") options.Silver = null;
            if (kind == "missing_field") options.Silver!.DiscountPercent = null;
            if (kind == "negative") options.Silver!.MinOrders = -1;
            if (kind == "crossed") options.Silver!.MinTotalSpent = 6000;
            if (kind == "precision") options.Gold!.DiscountPercent = 10.001m;
            var seeder = new DataSeeder(new UnitOfWork(context), Options.Create(options));
            Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
            Assert.That(await context.LoyaltyLevels.CountAsync(), Is.Zero);
            return true;
        });
    }

    [Test]
    public async Task PartialSeed_FailsWithoutRepair()
    {
        await Factory.ReadAsync(async context =>
        {
            context.LoyaltyLevels.RemoveRange(await context.LoyaltyLevels.Where(level => level.Name != "Basic").ToListAsync());
            await context.SaveChangesAsync();
            return true;
        });
        Assert.ThrowsAsync<InvalidOperationException>(() => Factory.SeedAgainAsync());
        Assert.That(await Factory.ReadAsync(context => context.LoyaltyLevels.CountAsync()), Is.EqualTo(1));
    }

    [TestCase("POST", "/api/loyalty-levels", HttpStatusCode.MethodNotAllowed)]
    [TestCase("DELETE", "/api/loyalty-levels/1", HttpStatusCode.MethodNotAllowed)]
    public Task LevelCreateAndDelete_AreAbsent(string method, string path, HttpStatusCode status) =>
        Expect(new HttpMethod(method), path, status);

    private static UpdateLoyaltyLevelsDto Write(IEnumerable<LoyaltyLevelDto> levels) => new()
    {
        Levels = levels.Select(level => new UpdateLoyaltyLevelDto
        {
            Id = level.Id, DiscountPercent = level.DiscountPercent, MinOrders = level.MinOrders, MinTotalSpent = level.MinTotalSpent
        }).ToArray()
    };
}
