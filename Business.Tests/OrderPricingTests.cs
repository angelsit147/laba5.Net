using System.Globalization;
using Business.Models;
using Business.Pricing;
using Business.Validation;
using NUnit.Framework;
using static Business.Tests.Fixtures.BusinessTestData;

namespace Business.Tests;

public class OrderPricingTests
{
    [TestCase("0", "200.00", "0")]
    [TestCase("100", "0", "200")]
    [TestCase("7.5", "185", "15")]
    [TestCase("7.25", "185.50", "14.50")]
    public void CalculatesWholeOrder(string discount, string total, string amount)
    {
        var result = OrderPricing.Calculate(new[] { (2, 80.25m), (1, 39.50m) }, decimal.Parse(discount, CultureInfo.InvariantCulture));
        Assert.That(result.Subtotal, Is.EqualTo(200m));
        Assert.That(result.Total, Is.EqualTo(decimal.Parse(total, CultureInfo.InvariantCulture)));
        Assert.That(result.DiscountAmount, Is.EqualTo(decimal.Parse(amount, CultureInfo.InvariantCulture)));
    }

    [Test]
    public void Discount_RoundsAwayFromZeroOncePerOrder()
    {
        Assert.That(OrderPricing.Calculate(new[] { (1, 0.10m) }, 5m).DiscountAmount, Is.EqualTo(0.01m));
        Assert.That(OrderPricing.Calculate(new[] { (1, 0.10m), (1, 0.10m) }, 5m).DiscountAmount, Is.EqualTo(0.01m));
        Assert.That(OrderPricing.Calculate(Array.Empty<(int, decimal)>(), 0).Total, Is.Zero);
        Assert.That(OrderPricing.LineTotal(1001, 1m), Is.EqualTo(1001m));
    }

    [TestCase("0")]
    [TestCase("-0.01")]
    [TestCase("0.001")]
    [TestCase("92233720368547758.08")]
    public void InvalidPrice_IsRejected(string text)
    {
        AssertError(() => Task.FromResult(OrderPricing.LineTotal(1, decimal.Parse(text, CultureInfo.InvariantCulture))),
            RestaurantErrorType.Validation);
    }

    [TestCase("-1")]
    [TestCase("100.01")]
    [TestCase("1.001")]
    public void InvalidPercent_IsRejected(string text)
    {
        AssertError(() => Task.FromResult(OrderPricing.Calculate(new[] { (1, 10m) },
            decimal.Parse(text, CultureInfo.InvariantCulture))), RestaurantErrorType.Validation);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonpositiveQuantity_IsRejected(int quantity)
    {
        AssertError(() => Task.FromResult(OrderPricing.LineTotal(quantity, 1m)), RestaurantErrorType.Validation);
    }

    [Test]
    public void ArithmeticOverflow_IsBusinessValidation()
    {
        var huge = (int.MaxValue, long.MaxValue / 100m);
        AssertError(() => Task.FromResult(OrderPricing.Calculate(Enumerable.Repeat(huge, 1000), 0m)),
            RestaurantErrorType.Validation, "money_overflow");
    }

    [Test]
    public void OrderWriteDtos_ContainOnlyAllowedFields()
    {
        Assert.That(typeof(CreateOrderDto).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "OrderType", "LoyaltyCode" }));
        Assert.That(typeof(UpdateDraftOrderDto).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "OrderType", "LoyaltyCode" }));
        Assert.That(typeof(AddOrderItemDto).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "MenuItemId", "Quantity" }));
        Assert.That(typeof(UpdateOrderItemDto).GetProperties().Select(property => property.Name), Is.EqualTo(new[] { "Quantity" }));
        Assert.That(typeof(OrderDto).GetProperties().Select(property => property.Name),
            Has.None.EqualTo("CustomerId").And.None.EqualTo("LoyaltyCode"));
    }
}
