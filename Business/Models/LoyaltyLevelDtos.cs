using System.Text.Json.Serialization;

namespace Business.Models;

public class LoyaltyLevelDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public int MinOrders { get; set; }
    public decimal MinTotalSpent { get; set; }
}

public class UpdateLoyaltyLevelsDto
{
    public IReadOnlyList<UpdateLoyaltyLevelDto> Levels { get; set; } = Array.Empty<UpdateLoyaltyLevelDto>();
}

public class UpdateLoyaltyLevelDto
{
    public int Id { get; set; }
    [JsonRequired]
    public decimal DiscountPercent { get; set; }
    [JsonRequired]
    public int MinOrders { get; set; }
    [JsonRequired]
    public decimal MinTotalSpent { get; set; }
}
