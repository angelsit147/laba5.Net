using Business.Models;

namespace Business.Interfaces;

public interface IOrderService
{
    Task<OrderDto> CreateAsync(CreateOrderDto dto, CancellationToken ct = default);
    Task<OrderDto> GetByIdAsync(int id, CancellationToken ct = default);
    Task<OrderDto> UpdateDraftAsync(int id, UpdateDraftOrderDto dto, CancellationToken ct = default);
    Task<OrderDto> AddItemAsync(int id, AddOrderItemDto dto, CancellationToken ct = default);
    Task<OrderDto> UpdateItemAsync(int id, int itemId, UpdateOrderItemDto dto, CancellationToken ct = default);
    Task RemoveItemAsync(int id, int itemId, CancellationToken ct = default);
    Task<OrderDto> ConfirmAsync(int id, CancellationToken ct = default);
    Task<OrderDto> CompleteAsync(int id, CancellationToken ct = default);
    Task<OrderDto> CancelAsync(int id, CancellationToken ct = default);
}
