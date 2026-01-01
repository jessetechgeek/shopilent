namespace Shopilent.Application.Features.Sales.Commands.MarkOrderAsReturned.V1;

public sealed record MarkOrderAsReturnedResponseV1
{
    public Guid OrderId { get; init; }
    public string Status { get; init; }
    public string ReturnReason { get; init; }
    public DateTime ReturnedAt { get; init; }
}
