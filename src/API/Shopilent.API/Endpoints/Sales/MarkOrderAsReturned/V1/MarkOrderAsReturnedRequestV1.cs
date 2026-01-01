namespace Shopilent.API.Endpoints.Sales.MarkOrderAsReturned.V1;

public class MarkOrderAsReturnedRequestV1
{
    public Guid OrderId { get; set; }
    public string ReturnReason { get; set; }
}
