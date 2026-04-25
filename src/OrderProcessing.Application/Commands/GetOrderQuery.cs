using MediatR;
using OrderProcessing.Application.Common;
using OrderProcessing.Application.DTOs;

namespace OrderProcessing.Application.Commands;

public record GetOrderQuery(Guid OrderId) : IRequest<Result<OrderResponse>>;

public record GetAllOrdersQuery() : IRequest<Result<IEnumerable<OrderResponse>>>;