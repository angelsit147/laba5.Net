using Business.Pricing;
using Data.Entities;

namespace Business.Validation;

public static class LoyaltyPolicyValidator
{
    public static IReadOnlyList<LoyaltyLevel> ValidateAndOrder(IReadOnlyCollection<LoyaltyLevel> levels)
    {
        const string invalidIdentity = "Loyalty levels must have unique positive Ids and unique nonempty names.";
        if (levels is null || levels.Count == 0 || levels.Any(level => level is null))
        {
            throw InvalidPolicy(invalidIdentity);
        }

        if (levels.Any(level => level.Id <= 0)
            || levels.Select(level => level.Id).Distinct().Count() != levels.Count)
        {
            throw InvalidPolicy(invalidIdentity);
        }

        if (levels.Any(level => string.IsNullOrWhiteSpace(level.Name) || level.Name.Length > 100)
            || levels.Select(level => level.Name).Distinct(StringComparer.Ordinal).Count() != levels.Count)
        {
            throw InvalidPolicy(invalidIdentity);
        }

        foreach (var level in levels)
        {
            OrderPricing.ValidateDiscountPercent(level.DiscountPercent);
            if (level.MinOrders < 0 || level.MinTotalSpent < 0 || level.MinTotalSpent > long.MaxValue / 100m
                || level.MinTotalSpent * 100m != decimal.Truncate(level.MinTotalSpent * 100m))
            {
                throw InvalidPolicy("Thresholds must be nonnegative and money must fit in Int64 cents with scale two.");
            }
        }

        var ordered = levels.OrderBy(level => level.MinOrders).ThenBy(level => level.MinTotalSpent).ToList();
        var basic = ordered[0];
        if (basic.Name != "Basic" || basic.MinOrders != 0 || basic.MinTotalSpent != 0 || basic.DiscountPercent != 0)
        {
            throw InvalidPolicy("Basic must be the initial level with zero thresholds and discount.");
        }

        for (var index = 1; index < ordered.Count; index++)
        {
            var previous = ordered[index - 1];
            var current = ordered[index];
            if (current.MinOrders < previous.MinOrders || current.MinTotalSpent < previous.MinTotalSpent
                || (current.MinOrders == previous.MinOrders && current.MinTotalSpent == previous.MinTotalSpent)
                || current.DiscountPercent < previous.DiscountPercent)
            {
                throw InvalidPolicy("Thresholds must form an increasing chain and discounts must not decrease.");
            }
        }

        return ordered;
    }

    private static RestaurantException InvalidPolicy(string message) =>
        new(RestaurantErrorType.Validation, "invalid_loyalty_policy", message);
}
