using FluentValidation;
using OrderProcessing.Application.DTOs;

namespace OrderProcessing.Application.Validators;

public class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(x => x.CustomerEmail)
            .NotEmpty().WithMessage("Customer email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.");

        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Customer name is required.")
            .MaximumLength(200).WithMessage("Customer name cannot exceed 200 characters.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Order must contain at least one item.")
            .Must(items => items.Count <= 50).WithMessage("Order cannot contain more than 50 items.");

        RuleForEach(x => x.Items).SetValidator(new OrderItemRequestValidator());

        RuleFor(x => x.Items)
            .Must(items => items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Duplicate products are not allowed in a single order.");

        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(128).WithMessage("Idempotency key cannot exceed 128 characters.")
            .When(x => x.IdempotencyKey != null);
    }
}

public class OrderItemRequestValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product ID is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero.")
            .LessThanOrEqualTo(1000).WithMessage("Quantity cannot exceed 1000 per item.");
    }
}