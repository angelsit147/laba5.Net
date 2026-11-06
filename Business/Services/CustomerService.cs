using System.Security.Cryptography;
using AutoMapper;
using Business.Interfaces;
using Business.Models;
using Business.Validation;
using Data.Entities;
using Data.Interfaces;

namespace Business.Services;

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IMapper mapper;

    public CustomerService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        this.unitOfWork = unitOfWork;
        this.mapper = mapper;
    }

    public async Task<CustomerRegistrationResultDto> RegisterAsync(
        CustomerRegistrationDto dto, CancellationToken ct = default)
    {
        if (dto is null)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_request",
                "Customer registration data is required.");
        }

        var firstName = ValidateName(dto.FirstName, "FirstName");
        var lastName = ValidateName(dto.LastName, "LastName");
        var phone = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
        if (phone?.Length > 32)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_phone",
                "Phone number cannot exceed 32 characters.");
        }

        var basic = await unitOfWork.LoyaltyLevels.FindByNameAsync("Basic", ct)
            ?? throw new RestaurantException(RestaurantErrorType.Conflict, "basic_level_missing",
                "The initial Basic loyalty level is not configured.");
        if (basic.DiscountPercent != 0 || basic.MinOrders != 0 || basic.MinTotalSpent != 0)
        {
            throw new RestaurantException(RestaurantErrorType.Conflict, "basic_level_invalid",
                "Basic must have zero thresholds and discount.");
        }

        var customer = new Customer
        {
            FirstName = firstName, LastName = lastName, PhoneNumber = phone,
            LoyaltyCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            LoyaltyLevelId = basic.Id, LoyaltyLevel = basic
        };
        await unitOfWork.Customers.AddAsync(customer, ct);
        await unitOfWork.SaveAsync(ct);
        return mapper.Map<CustomerRegistrationResultDto>(customer);
    }

    public async Task<LoyaltyCardDto> ResolveLoyaltyAsync(LoyaltyCodeDto dto, CancellationToken ct = default)
    {
        var code = dto?.LoyaltyCode?.Trim().ToUpperInvariant();
        if (code is null || code.Length != 32 || code.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_loyalty_code",
                "Loyalty code must contain 32 hexadecimal characters.");
        }

        var customer = await unitOfWork.Customers.FindByLoyaltyCodeAsync(code, ct)
            ?? throw new RestaurantException(RestaurantErrorType.NotFound, "loyalty_code_not_found",
                "Loyalty card was not found.");
        return mapper.Map<LoyaltyCardDto>(customer);
    }

    private static string ValidateName(string? value, string field)
    {
        var name = value?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_name",
                field + " must contain 1 to 100 characters.");
        }

        return name;
    }
}
