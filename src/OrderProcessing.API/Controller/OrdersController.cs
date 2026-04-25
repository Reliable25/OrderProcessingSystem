using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Application.Commands;
using OrderProcessing.Application.DTOs;

namespace OrderProcessing.API.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IValidator<PlaceOrderRequest> _validator;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IMediator mediator, IValidator<PlaceOrderRequest> validator, ILogger<OrdersController> logger)
        {
            _mediator = mediator;
            _validator = validator;
            _logger = logger;
        }

        /// <summary>
        /// Place a new order.
        /// </summary>
        /// <remarks>
        /// Validates product existence and stock availability. Supports client-provided idempotency key
        /// so retries return the same result. Returns 201 with created order on success.
        /// </remarks>
        /// <param name="request">Order payload including customer and items.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Created order or error information.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(OrderResponse), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequest request, CancellationToken cancellationToken)
        {
            var validation = await _validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
                return BadRequest(new { Errors = validation.Errors.Select(e => e.ErrorMessage) });

            _logger.LogInformation("Received PlaceOrder request for {Email} with {Items} items", request.CustomerEmail, request.Items?.Count ?? 0);

            var result = await _mediator.Send(new PlaceOrderCommand(request), cancellationToken);
            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetOrder), new { id = result.Value!.Id }, result.Value);

            return result.ErrorCode switch
            {
                nameof(OrderProcessing.Application.Common.ErrorCodes.ProductNotFound) => NotFound(new { result.ErrorMessage }),
                nameof(OrderProcessing.Application.Common.ErrorCodes.InsufficientStock) => Conflict(new { result.ErrorMessage }),
                nameof(OrderProcessing.Application.Common.ErrorCodes.DuplicateOrder) => Conflict(new { result.ErrorMessage }),
                _ => BadRequest(new { result.ErrorMessage })
            };
        }

        /// <summary>
        /// Get an order by id.
        /// </summary>
        /// <param name="id">Order GUID.</param>
        /// <returns>Order details or 404 if not found.</returns>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(OrderResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetOrder(Guid id)
        {
            var result = await _mediator.Send(new GetOrderQuery(id));
            if (!result.IsSuccess)
                return NotFound(new { result.ErrorMessage });

            return Ok(result.Value);
        }
    }
}