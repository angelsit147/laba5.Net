namespace Business.Models;

public class CustomerRegistrationDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

public class CustomerRegistrationResultDto
{
    public string LoyaltyCode { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
}

public class LoyaltyCodeDto
{
    public string LoyaltyCode { get; set; } = string.Empty;
}

public class LoyaltyCardDto
{
    public string LevelName { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
}
