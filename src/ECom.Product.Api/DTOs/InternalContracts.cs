namespace ECom.Product.Api.DTOs;

using System.ComponentModel.DataAnnotations;

public sealed record ProductBatchRequest
{
    [Required]
    public IReadOnlyList<Guid> Ids { get; init; } = Array.Empty<Guid>();
}

public sealed record StockLine(Guid ProductId, int Quantity);

public sealed record StockAdjustmentRequest
{
    [Required]
    public Guid OrderId { get; init; }

    [Required]
    [MinLength(1, ErrorMessage = "At least one item is required.")]
    public IReadOnlyList<StockLine> Items { get; init; } = Array.Empty<StockLine>();
}
