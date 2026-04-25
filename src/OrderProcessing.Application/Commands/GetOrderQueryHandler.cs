using MediatR;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Common;
using OrderProcessing.Application.DTOs;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Application.Commands;

public class GetOrderQueryHandler :
    IRequestHandler<GetOrderQuery, Result<OrderResponse>>,
    IRequestHandler<GetAllOrdersQuery, Result<IEnumerable<OrderResponse>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GetOrderQueryHandler> _logger;

    public GetOrderQueryHandler(IUnitOfWork unitOfWork, ILogger<GetOrderQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<OrderResponse>> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(request.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found.", request.OrderId);
            return Result<OrderResponse>.Failure($"Order '{request.OrderId}' not found.", ErrorCodes.OrderNotFound);
        }

        return Result<OrderResponse>.Success(MapToResponse(order));
    }

    public async Task<Result<IEnumerable<OrderResponse>>> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _unitOfWork.Orders.GetAllAsync(cancellationToken);
        return Result<IEnumerable<OrderResponse>>.Success(orders.Select(MapToResponse));
    }

    private static OrderResponse MapToResponse(Domain.Entities.Order order) => new()
    {
        Id = order.Id,
        CustomerEmail = order.CustomerEmail,
        CustomerName = order.CustomerName,
        Status = order.Status,
        TotalAmount = order.TotalAmount,
        IdempotencyKey = order.IdempotencyKey,
        FailureReason = order.FailureReason,
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
        Items = order.Items.Select(i => new OrderItemResponse
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity,
            TotalPrice = i.TotalPrice
        }).ToList()
    };
}