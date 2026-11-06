using Business.Models;
using Business.Services;
using Business.Tests.Fixtures;
using Business.Validation;
using Data.Entities;
using Data.Interfaces;
using Moq;
using NUnit.Framework;
using static Business.Tests.Fixtures.BusinessTestData;

namespace Business.Tests;

public class CustomerServiceTests
{
    private Mock<IUnitOfWork> unitOfWork = null!;
    private Mock<ICustomerRepository> customers = null!;
    private Mock<ILoyaltyLevelRepository> levels = null!;
    private CustomerService service = null!;
    private readonly CancellationToken ct = new CancellationTokenSource().Token;

    [SetUp]
    public void SetUp()
    {
        unitOfWork = CreateUnitOfWork();
        customers = Mock.Get(unitOfWork.Object.Customers);
        levels = Mock.Get(unitOfWork.Object.LoyaltyLevels);
        levels.Setup(value => value.FindByNameAsync("Basic", ct)).ReturnsAsync(new LoyaltyLevel { Id = 77, Name = "Basic" });
        service = new CustomerService(unitOfWork.Object, Mapper);
    }

    private static CustomerRegistrationDto Registration() =>
        new() { FirstName = " Test ", LastName = " Customer ", PhoneNumber = " 123 " };

    [Test]
    public async Task Register_UsesRepositoryBasicAndGeneratesDistinctCanonicalCodes()
    {
        var created = new List<Customer>();
        customers.Setup(value => value.AddAsync(It.IsAny<Customer>(), ct))
            .Callback<Customer, CancellationToken>((entity, _) => created.Add(entity))
            .Returns(Task.CompletedTask);

        var first = await service.RegisterAsync(Registration(), ct);
        var second = await service.RegisterAsync(Registration(), ct);

        Assert.That(created, Has.Count.EqualTo(2));
        Assert.That(created.All(entity => entity.LoyaltyLevelId == 77 && entity.FirstName == "Test"
            && entity.LastName == "Customer" && entity.PhoneNumber == "123"), Is.True);
        Assert.That(first.LoyaltyCode, Does.Match("^[0-9A-F]{32}$"));
        Assert.That(second.LoyaltyCode, Does.Match("^[0-9A-F]{32}$"));
        Assert.That(second.LoyaltyCode, Is.Not.EqualTo(first.LoyaltyCode));
        Assert.That(first.LevelName, Is.EqualTo("Basic"));
        Assert.That(first.DiscountPercent, Is.Zero);
        Assert.That(first.LoyaltyCode, Is.EqualTo(created[0].LoyaltyCode));
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Exactly(2));
        customers.Verify(value => value.FindByLoyaltyCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task OptionalPhone_NormalizesToNull(string? phone)
    {
        Customer? saved = null;
        customers.Setup(value => value.AddAsync(It.IsAny<Customer>(), ct))
            .Callback<Customer, CancellationToken>((entity, _) => saved = entity)
            .Returns(Task.CompletedTask);
        var dto = Registration();
        dto.PhoneNumber = phone;

        await service.RegisterAsync(dto, ct);

        Assert.That(saved!.PhoneNumber, Is.Null);
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
    }

    [TestCaseSource(typeof(BusinessTestData), nameof(InvalidNames))]
    public void InvalidNames_DoNotRegister(string? name)
    {
        var dto = Registration();
        dto.FirstName = name!;
        AssertError(async () => await service.RegisterAsync(dto, ct), RestaurantErrorType.Validation);
        dto = Registration();
        dto.LastName = name!;
        AssertError(async () => await service.RegisterAsync(dto, ct), RestaurantErrorType.Validation);
        customers.Verify(value => value.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoSave(unitOfWork);
    }

    [Test]
    public async Task LengthBoundaries_AreEnforced()
    {
        var dto = new CustomerRegistrationDto
        {
            FirstName = new string('f', 100), LastName = new string('l', 100), PhoneNumber = new string('1', 32)
        };
        await service.RegisterAsync(dto, ct);
        dto.PhoneNumber += "1";
        AssertError(async () => await service.RegisterAsync(dto, ct), RestaurantErrorType.Validation, "invalid_phone");
        AssertError(async () => await service.RegisterAsync(null!, ct), RestaurantErrorType.Validation);
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
    }

    [TestCase("missing")]
    [TestCase("discount")]
    [TestCase("orders")]
    [TestCase("spent")]
    public void MissingOrInvalidBasic_DoesNotRegister(string scenario)
    {
        LoyaltyLevel? basic = scenario == "missing" ? null : new LoyaltyLevel
        {
            Id = 77, Name = "Basic", DiscountPercent = scenario == "discount" ? 1m : 0,
            MinOrders = scenario == "orders" ? 1 : 0, MinTotalSpent = scenario == "spent" ? 1m : 0
        };
        levels.Setup(value => value.FindByNameAsync("Basic", ct)).ReturnsAsync(basic);

        AssertError(async () => await service.RegisterAsync(Registration(), ct), RestaurantErrorType.Conflict);
        customers.Verify(value => value.AddAsync(It.IsAny<Customer>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoSave(unitOfWork);
    }

    [Test]
    public async Task Resolve_NormalizesCodeAndReturnsOnlyCardData()
    {
        var code = new string('A', 32);
        customers.Setup(value => value.FindByLoyaltyCodeAsync(code, ct)).ReturnsAsync(new Customer
        {
            Id = 42, FirstName = "Private", LastName = "Person", PhoneNumber = "Secret",
            LoyaltyCode = code, LoyaltyLevel = new LoyaltyLevel { Name = "Silver", DiscountPercent = 7.25m }
        });

        var result = await service.ResolveLoyaltyAsync(new LoyaltyCodeDto { LoyaltyCode = " " + code.ToLowerInvariant() + " " }, ct);

        Assert.That(result.LevelName, Is.EqualTo("Silver"));
        Assert.That(result.DiscountPercent, Is.EqualTo(7.25m));
        Assert.That(typeof(LoyaltyCardDto).GetProperties().Select(property => property.Name),
            Is.EquivalentTo(new[] { "LevelName", "DiscountPercent" }));
        VerifyNoSave(unitOfWork);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase("ABC")]
    [TestCase("GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG")]
    [TestCase("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void InvalidCode_IsValidationWithoutLookup(string? code)
    {
        AssertError(async () => await service.ResolveLoyaltyAsync(new LoyaltyCodeDto { LoyaltyCode = code! }, ct),
            RestaurantErrorType.Validation, "invalid_loyalty_code");
        AssertError(async () => await service.ResolveLoyaltyAsync(null!, ct), RestaurantErrorType.Validation);
        customers.Verify(value => value.FindByLoyaltyCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNoSave(unitOfWork);
    }

    [Test]
    public void UnknownValidCode_IsNotFound()
    {
        AssertError(async () => await service.ResolveLoyaltyAsync(
            new LoyaltyCodeDto { LoyaltyCode = new string('F', 32) }, ct),
            RestaurantErrorType.NotFound, "loyalty_code_not_found");
        VerifyNoSave(unitOfWork);
    }

    [Test]
    public void SaveFailure_IsPropagatedWithoutRetry()
    {
        var failure = new InvalidOperationException("Storage failure");
        unitOfWork.Setup(value => value.SaveAsync(ct)).ThrowsAsync(failure);

        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.RegisterAsync(Registration(), ct)), Is.SameAs(failure));
        unitOfWork.Verify(value => value.SaveAsync(ct), Times.Once);
        customers.Verify(value => value.AddAsync(It.IsAny<Customer>(), ct), Times.Once);
    }
}
