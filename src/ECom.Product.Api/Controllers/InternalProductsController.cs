namespace ECom.Product.Api.Controllers;

using ECom.Product.Api.DTOs;
using ECom.Product.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// East-west endpoints used by the Cart and Order services. Not routed by the gateway.
/// Callers forward the end user's JWT.
/// </summary>
[ApiController]
[Route("internal")]
[Authorize]
public sealed class InternalProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IStockService _stockService;

    public InternalProductsController(IProductService productService, IStockService stockService)
    {
        _productService = productService;
        _stockService = stockService;
    }

    /// <summary>Returns every existing product among <c>Ids</c>, regardless of stock level.</summary>
    [HttpPost("products/batch")]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBatch([FromBody] ProductBatchRequest request, CancellationToken cancellationToken)
    {
        var products = await _productService.GetProductsByIdsAsync(request.Ids, cancellationToken);
        return Ok(products);
    }

    [HttpPost("stock/decrement")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Decrement([FromBody] StockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        await _stockService.DecrementAsync(request.OrderId, request.Items, cancellationToken);
        return NoContent();
    }

    [HttpPost("stock/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Restore([FromBody] StockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        await _stockService.RestoreAsync(request.OrderId, request.Items, cancellationToken);
        return NoContent();
    }
}
