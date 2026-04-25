using MediatR;
using OrderProcessing.Application.Common;
using OrderProcessing.Application.DTOs;

namespace OrderProcessing.Application.Commands;

public record PlaceOrderCommand(PlaceOrderRequest Request) : IRequest<Result<OrderResponse>>;

