using Business.Validation;
using Data.Entities;

namespace Business.Pricing;

public static class OrderPricing
{
    public static decimal LineTotal(int quantity, decimal unitPrice)
    {
        if (quantity <= 0)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_quantity",
                "Quantity must be a positive int.");
        }

        if (unitPrice <= 0 || unitPrice > long.MaxValue / 100m
            || unitPrice * 100m != decimal.Truncate(unitPrice * 100m))
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_price",
                "Price must be positive, fit in Int64 cents and have at most two fractional digits.");
        }

        try
        {
            return checked(quantity * unitPrice);
        }
        catch (OverflowException)
        {
            throw MoneyOverflow();
        }
    }

    public static (decimal Subtotal, decimal DiscountAmount, decimal Total) Calculate(
        IEnumerable<(int Quantity, decimal UnitPrice)> items, decimal discountPercent)
    {
        ValidateDiscountPercent(discountPercent);
        ArgumentNullException.ThrowIfNull(items);
        try
        {
            var subtotal = items.Aggregate(0m,
                (sum, item) => checked(sum + LineTotal(item.Quantity, item.UnitPrice)));
            var discount = decimal.Round(checked(subtotal * discountPercent) / 100m,
                2, MidpointRounding.AwayFromZero);
            return (subtotal, discount, subtotal - discount);
        }
        catch (OverflowException)
        {
            throw MoneyOverflow();
        }
    }

    public static decimal HistoricalTotal(Order order)
    {
        if (!order.DiscountPercentFixed.HasValue)
        {
            throw new RestaurantException(RestaurantErrorType.Conflict, "missing_fixed_discount",
                "A confirmed or completed order must have a fixed discount.");
        }

        return Calculate(order.Items.Select(item => (item.Quantity, item.UnitPrice)),
            order.DiscountPercentFixed.Value).Total;
    }

    public static void ValidateDiscountPercent(decimal value)
    {
        if (value < 0 || value > 100 || value * 100m != decimal.Truncate(value * 100m))
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_discount",
                "Discount must be between 0 and 100 percent with at most two fractional digits.");
        }
    }

    private static RestaurantException MoneyOverflow() =>
        new(RestaurantErrorType.Validation, "money_overflow", "The monetary calculation exceeds decimal capacity.");
}
