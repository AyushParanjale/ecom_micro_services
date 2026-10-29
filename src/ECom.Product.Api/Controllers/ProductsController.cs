namespace ECom.Product.Api.Controllers;

using ECom.Product.Api.DTOs;
using ECom.Product.Api.Services;
using ECom.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService) => _productService = productService;

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productService.CreateProductAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        var products = await _productService.GetProductsAsync(category, cancellationToken);
        return Ok(products);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductByIdAsync(id, cancellationToken);
        return Ok(product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productService.UpdateProductAsync(id, request, cancellationToken);
        return Ok(product);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _productService.DeleteProductAsync(id, cancellationToken);
        return NoContent();
    }
}
