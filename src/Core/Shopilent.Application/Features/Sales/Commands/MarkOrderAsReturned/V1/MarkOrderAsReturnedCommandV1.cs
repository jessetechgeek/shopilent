using Shopilent.Application.Abstractions.Messaging;

namespace Shopilent.Application.Features.Sales.Commands.MarkOrderAsReturned.V1;

public sealed record MarkOrderAsReturnedCommandV1 : ICommand<MarkOrderAsReturnedResponseV1>
{
    public Guid OrderId { get; init; }
    public string ReturnReason { get; init; }
}
