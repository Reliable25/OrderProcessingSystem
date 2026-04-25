using MediatR;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Common;
using OrderProcessing.Application.DTOs;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Application.Commands;

// ── Queries ──────────────────────────────────────────────────────────────────
public record GetAllProductsQuery() : IRequest<Result<IEnumerable<ProductResponse>>>;
public record GetProductQuery(Guid ProductId) : IRequest<Result<ProductResponse>>;

// ── Commands ─────────────────────────────────────────────────────────────────
public record CreateProductCommand(CreateProductRequest Request) : IRequest<Result<ProductResponse>>;

// ── Handlers ─────────────────────────────────────────────────────────────────
public class ProductQueryHandler :
    IRequestHandler<GetAllProductsQuery, Result<IEnumerable<ProductResponse>>>,
    IRequestHandler<GetProductQuery, Result<ProductResponse>>
{
    private readonly IUnitOfWork _unitOfWork;

    public ProductQueryHandler(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<Result<IEnumerable<ProductResponse>>> Handle(GetAllProductsQuery request, CancellationToken cancellationToken)
    {
        var products = await _unitOfWork.Products.GetAllActiveAsync(cancellationToken);
        return Result<IEnumerable<ProductResponse>>.Success(products.Select(MapToResponse));
    }

    public async Task<Result<ProductResponse>> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(request.ProductId, cancellationToken);
        if (product == null)
            return Result<ProductResponse>.Failure($"Product '{request.ProductId}' not found.", ErrorCodes.ProductNotFound);

        return Result<ProductResponse>.Success(MapToResponse(product));
    }

    private static ProductResponse MapToResponse(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Price = p.Price,
        StockQuantity = p.StockQuantity,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<ProductResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(IUnitOfWork unitOfWork, ILogger<CreateProductCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<ProductResponse>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;
        var product = new Product(req.Name, req.Description, req.Price, req.StockQuantity);
        await _unitOfWork.Products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Product {ProductId} '{Name}' created with stock {Stock}.", product.Id, product.Name, product.StockQuantity);

        return Result<ProductResponse>.Success(new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            IsActive = product.IsActive,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        });
    }
}