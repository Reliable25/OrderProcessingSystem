using Microsoft.AspNetCore.Mvc;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.API.Controller;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IUnitOfWork uow, ILogger<ProductsController> logger) => (_uow, _logger) = (uow, logger);

    /// <summary>
    /// Returns all active products with current stock.
    /// Useful to discover product ids for placing orders.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetAll()
    {
        var prods = await _uow.Products.GetAllActiveAsync();
        var result = prods.Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            p.Price,
            p.StockQuantity
        });
        return Ok(result);
    }

    /// <summary>
    /// Returns a single product by id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get(Guid id)
    {
        var p = await _uow.Products.GetByIdAsync(id);
        if (p == null) return NotFound();
        return Ok(new { p.Id, p.Name, p.Description, p.Price, p.StockQuantity });
    }
}
