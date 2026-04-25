using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Common;
using OrderProcessing.Application.DTOs;
using OrderProcessing.Application.EventHandlers;
using OrderProcessing.Application.Utils;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Events;
using OrderProcessing.Domain.Interfaces;
using System.Text.Json;

namespace OrderProcessing.Application.Commands;

public class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Result<OrderResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PlaceOrderCommandHandler> _logger;
    private readonly OrderPlacedEventHandler _orderPlacedHandler;

    public PlaceOrderCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<PlaceOrderCommandHandler> logger,
        OrderPlacedEventHandler orderPlacedHandler)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _orderPlacedHandler = orderPlacedHandler;
    }

    public async Task<Result<OrderResponse>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request ?? throw new ArgumentNullException(nameof(request.Request));
        var key = req.IdempotencyKey;
        var requestHash = IdempotencyHelper.ComputeHash(req);

        // Idempotency handling: try to insert an InProgress record; if already exists, behave deterministically.
        if (!string.IsNullOrWhiteSpace(key))
        {
            try
            {
                var rec = new IdempotencyRecord(key, requestHash);
                await _unitOfWork.Idempotency.AddAsync(rec, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken); // persist lock
            }
            catch (DbUpdateException)
            {
                // another request created record concurrently - reload
                var existing = await _unitOfWork.Idempotency.GetByKeyAsync(key, cancellationToken);
                if (existing != null)
                {
                    if (existing.Status == IdempotencyStatus.Completed)
                    {
                        if (existing.RequestHash == requestHash && existing.ResourceId.HasValue)
                        {
                            var existingOrder = await _unitOfWork.Orders.GetByIdAsync(existing.ResourceId.Value, cancellationToken);
                            return Result<OrderResponse>.Success(MapToResponse(existingOrder!));
                        }

                        return Result<OrderResponse>.Failure("Idempotency key already used with different payload.", ErrorCodes.DuplicateOrder);
                    }

                    // InProgress or Failed: treat as concurrent-processing
                    return Result<OrderResponse>.Failure("Request with same idempotency key is already being processed.", ErrorCodes.DuplicateOrder);
                }

                // If we cannot determine existing record, fallthrough and attempt safe path
            }
        }

        try
        {
            // All reservation + order creation happens inside a serializable transaction to avoid oversells.
            var createdOrder = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // 1) Lock and reserve each product
                var productIds = req.Items.Select(i => i.ProductId).Distinct().ToList();

                var lockedProducts = new Dictionary<Guid, Product>();
                foreach (var pid in productIds)
                {
                    var prod = await _unitOfWork.Products.GetByIdWithLockAsync(pid, cancellationToken);
                    if (prod == null)
                        throw new InvalidOperationException($"{ErrorCodes.ProductNotFound}:{pid}");

                    lockedProducts[pid] = prod;
                }

                // 2) Validate quantities and reserve
                foreach (var item in req.Items)
                {
                    var prod = lockedProducts[item.ProductId];
                    if (!prod.TryReserveStock(item.Quantity))
                        throw new InvalidOperationException($"{ErrorCodes.InsufficientStock}:{item.ProductId}");

                    await _unitOfWork.Products.UpdateAsync(prod, cancellationToken);
                }

                // 3) Create order aggregate
                var order = new Order(req.CustomerEmail, req.CustomerName, req.IdempotencyKey);
                foreach (var item in req.Items)
                {
                    var prod = lockedProducts[item.ProductId];
                    order.AddItem(prod.Id, prod.Name, prod.Price, item.Quantity);
                }

                order.Confirm(); // creates OrderPlacedEvent inside aggregate
                await _unitOfWork.Orders.AddAsync(order, cancellationToken);

                // Return aggregate so the UnitOfWork will save & commit
                return order;
            }, cancellationToken);

            // After successful commit, update idempotency record as Completed (if idempotency used)
            if (!string.IsNullOrWhiteSpace(key))
            {
                var rec = await _unitOfWork.Idempotency.GetByKeyAsync(key, cancellationToken);
                if (rec != null)
                {
                    rec.MarkCompleted(createdOrder.Id, JsonSerializer.Serialize(MapToResponse(createdOrder)));
                    await _unitOfWork.Idempotency.UpdateAsync(rec, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
            }

            // Publish domain event(s)
            var evt = createdOrder.DomainEvents.OfType<OrderPlacedEvent>().FirstOrDefault();
            if (evt != null)
            {
                await _orderPlacedHandler.HandleAsync(evt, cancellationToken);
                createdOrder.ClearDomainEvents();
                await _unitOfWork.Orders.UpdateAsync(createdOrder, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result<OrderResponse>.Success(MapToResponse(createdOrder));
        }
        catch (InvalidOperationException invEx) when (invEx.Message.StartsWith(ErrorCodes.ProductNotFound) || invEx.Message.StartsWith(ErrorCodes.InsufficientStock))
        {
            var parts = invEx.Message.Split(':', 2);
            var code = parts[0];
            var detail = parts.Length > 1 ? parts[1] : invEx.Message;
            _logger.LogWarning("Order placement failed: {Msg}", invEx.Message);
            return Result<OrderResponse>.Failure(code == ErrorCodes.ProductNotFound ? $"Product not found ({detail})" : $"Insufficient stock for product ({detail})", code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while placing order.");
            // If idempotency record existed, mark failed to allow retries/cleanup
            if (!string.IsNullOrWhiteSpace(key))
            {
                    var rec = await _unitOfWork.Idempotency.GetByKeyAsync(key, CancellationToken.None);
                    if (rec != null)
                    {
                        rec.MarkFailed($"{ex.GetType().Name}: {ex.Message}");
                        await _unitOfWork.Idempotency.UpdateAsync(rec, CancellationToken.None);
                        await _unitOfWork.SaveChangesAsync(CancellationToken.None);
                    }
            }

            return Result<OrderResponse>.Failure("Unexpected error while placing order.", ErrorCodes.InvalidOperation);
        }
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