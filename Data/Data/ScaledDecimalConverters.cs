using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Data.Data;

public static class ScaledDecimalConverters
{
    public static ValueConverter<decimal, long> Money { get; } =
        new(value => ToMoneyUnits(value), value => value / 100m);

    public static ValueConverter<decimal, int> Percent { get; } =
        new(value => ToPercentUnits(value), value => value / 100m);

    private static long ToMoneyUnits(decimal value)
    {
        if (value < 0 || value > long.MaxValue / 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Money must fit in nonnegative Int64 cents.");
        }

        var scaled = value * 100m;
        if (scaled != decimal.Truncate(scaled))
        {
            throw new ArgumentException("Money must have at most two fractional digits.", nameof(value));
        }

        return checked((long)scaled);
    }

    private static int ToPercentUnits(decimal value)
    {
        if (value < 0 || value > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Percent must be between 0 and 100.");
        }

        var scaled = value * 100m;
        if (scaled != decimal.Truncate(scaled))
        {
            throw new ArgumentException("Percent must have at most two fractional digits.", nameof(value));
        }

        return checked((int)scaled);
    }
}
