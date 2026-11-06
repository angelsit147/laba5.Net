using Business.Interfaces;
using Business.Models;
using Business.Services;
using Business.Validation;
using Data.Entities;
using Data.Enums;
using Moq;
using NUnit.Framework;
using static Business.Tests.Fixtures.BusinessTestData;

namespace Business.Tests;

public class OrderStatusTests
{
    private static IEnumerable<TestCaseData> Transitions()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            foreach (var command in new[] { "confirm", "complete", "cancel" })
            {
                yield return new TestCaseData(status, command);
            }
        }
    }

    [TestCaseSource(nameof(Transitions))]
    public async Task OnlyAllowedTransitions_AreAccepted(OrderStatus status, string command)
    {
        var work = CreateUnitOfWork();
        var order = TestOrder(status);
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var loyalty = new Mock<ILoyaltyService>();
        var service = new OrderService(work.Object, loyalty.Object);
        var allowed = (command == "confirm" && status == OrderStatus.Draft)
            || (command == "complete" && status == OrderStatus.Confirmed)
            || (command == "cancel" && status is OrderStatus.Draft or OrderStatus.Confirmed);

        Task<OrderDto> Invoke() => command switch
        {
            "confirm" => service.ConfirmAsync(7),
            "complete" => service.CompleteAsync(7),
            _ => service.CancelAsync(7)
        };

        if (!allowed)
        {
            AssertError(async () => await Invoke(), RestaurantErrorType.Conflict, "invalid_order_state");
            Assert.That(order.Status, Is.EqualTo(status));
            work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            var result = await Invoke();
            Assert.That(result.Status, Is.EqualTo(command == "confirm" ? OrderStatus.Confirmed :
                command == "complete" ? OrderStatus.Completed : OrderStatus.Cancelled));
            work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
            AssertError(async () => await Invoke(), RestaurantErrorType.Conflict, "invalid_order_state");
            work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
            if (command == "complete")
            {
                loyalty.Verify(value => value.RecalculateCustomerAsync(42, order, It.IsAny<CancellationToken>()), Times.Once);
            }
        }
    }

    [TestCase(OrderStatus.Confirmed)]
    [TestCase(OrderStatus.Completed)]
    [TestCase(OrderStatus.Cancelled)]
    public void NonDraft_RejectsEveryEdit(OrderStatus status)
    {
        var work = CreateUnitOfWork();
        var order = TestOrder(status);
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var service = new OrderService(work.Object, Mock.Of<ILoyaltyService>());
        AssertError(async () => await service.UpdateDraftAsync(7, new UpdateDraftOrderDto()), RestaurantErrorType.Conflict);
        AssertError(async () => await service.AddItemAsync(7, new AddOrderItemDto { MenuItemId = 3, Quantity = 1 }), RestaurantErrorType.Conflict);
        AssertError(async () => await service.UpdateItemAsync(7, 71, new UpdateOrderItemDto { Quantity = 1 }), RestaurantErrorType.Conflict);
        AssertError(async () => await service.RemoveItemAsync(7, 71), RestaurantErrorType.Conflict);
        Assert.That(order.Items.Single().Quantity, Is.EqualTo(2));
        VerifyNoSave(work);
    }

    [Test]
    public void EmptyDraft_CannotBeConfirmed()
    {
        var work = CreateUnitOfWork();
        var order = TestOrder();
        order.Items.Clear();
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        AssertError(async () => await new OrderService(work.Object, Mock.Of<ILoyaltyService>()).ConfirmAsync(7),
            RestaurantErrorType.Conflict, "empty_order");
        work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GuestComplete_DoesNotRecalculateCustomer()
    {
        var work = CreateUnitOfWork();
        var order = TestOrder(OrderStatus.Confirmed, false);
        Mock.Get(work.Object.Orders).Setup(value => value.GetWithCurrentMenuAsync(7, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        var loyalty = new Mock<ILoyaltyService>();
        await new OrderService(work.Object, loyalty.Object).CompleteAsync(7);
        loyalty.Verify(value => value.RecalculateCustomerAsync(It.IsAny<int>(), It.IsAny<Order>(),
            It.IsAny<CancellationToken>()), Times.Never);
        work.Verify(value => value.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
