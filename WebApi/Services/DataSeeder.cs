using Business.Validation;
using Data.Entities;
using Data.Interfaces;
using Microsoft.Extensions.Options;
using WebApi.Configuration;

namespace WebApi.Services;

public class DataSeeder
{
    private readonly IUnitOfWork unitOfWork;
    private readonly LoyaltySeedOptions options;

    public DataSeeder(IUnitOfWork unitOfWork, IOptions<LoyaltySeedOptions> options)
    {
        this.unitOfWork = unitOfWork;
        this.options = options.Value;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var existing = await unitOfWork.LoyaltyLevels.GetAllAsync(ct);
        if (existing.Count > 0)
        {
            if (!new[] { "Basic", "Silver", "Gold" }.All(name => existing.Any(level => level.Name == name)))
            {
                throw new InvalidOperationException("The initial loyalty levels are incomplete. Basic, Silver and Gold must all exist.");
            }

            return;
        }

        var levels = new[]
        {
            new LoyaltyLevel { Id = 1, Name = "Basic" },
            FromOptions(2, "Silver", options.Silver),
            FromOptions(3, "Gold", options.Gold)
        };
        try
        {
            LoyaltyPolicyValidator.ValidateAndOrder(levels);
        }
        catch (RestaurantException)
        {
            throw new InvalidOperationException("LoyaltySeed configuration must form a valid policy with a zero Basic level and increasing thresholds.");
        }

        foreach (var level in levels)
        {

            level.Id = 0;
            await unitOfWork.LoyaltyLevels.AddAsync(level, ct);
        }

        await unitOfWork.SaveAsync(ct);
    }

    private static LoyaltyLevel FromOptions(int id, string name, LoyaltySeedOptions.LevelOptions? values)
    {
        if (values?.MinOrders is null || values.MinTotalSpent is null || values.DiscountPercent is null)
        {
            throw new InvalidOperationException("Configure LoyaltySeed:Silver and LoyaltySeed:Gold with MinOrders, MinTotalSpent and DiscountPercent before seeding an empty database.");
        }

        return new LoyaltyLevel
        {
            Id = id, Name = name, MinOrders = values.MinOrders.Value,
            MinTotalSpent = values.MinTotalSpent.Value, DiscountPercent = values.DiscountPercent.Value
        };
    }
}
