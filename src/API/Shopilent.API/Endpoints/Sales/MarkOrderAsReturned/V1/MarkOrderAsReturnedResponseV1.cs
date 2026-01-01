namespace Shopilent.API.Endpoints.Sales.MarkOrderAsReturned.V1;

public class MarkOrderAsReturnedResponseV1
{
    public Guid OrderId { get; set; }
    public string Status { get; set; }
    public string ReturnReason { get; set; }
    public DateTime ReturnedAt { get; set; }
}
