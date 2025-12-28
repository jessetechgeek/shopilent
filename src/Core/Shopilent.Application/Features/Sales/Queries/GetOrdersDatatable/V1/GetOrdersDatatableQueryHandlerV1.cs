using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Models;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Read;

namespace Shopilent.Application.Features.Sales.Queries.GetOrdersDatatable.V1;

internal sealed class GetOrdersDatatableQueryHandlerV1 :
    IQueryHandler<GetOrdersDatatableQueryV1, DataTableResult<OrderDatatableDto>>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly IOrderReadRepository _orderReadRepository;
    private readonly ILogger<GetOrdersDatatableQueryHandlerV1> _logger;

    public GetOrdersDatatableQueryHandlerV1(
        IUserReadRepository userReadRepository,
        IOrderReadRepository orderReadRepository,
        ILogger<GetOrdersDatatableQueryHandlerV1> logger)
    {
        _userReadRepository = userReadRepository;
        _orderReadRepository = orderReadRepository;
        _logger = logger;
    }

    public async Task<Result<DataTableResult<OrderDatatableDto>>> Handle(
        GetOrdersDatatableQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get datatable results from repository
            var result = await _orderReadRepository.GetOrderDetailDataTableAsync(
                request.Request,
                cancellationToken);

            // Map to OrderDatatableDto
            var dtoItems = new List<OrderDatatableDto>();
            foreach (var order in result.Data)
            {
                // Get user information if available
                string userEmail = null;
                string userFullName = null;
                if (order.UserId.HasValue)
                {
                    var user = await _userReadRepository.GetByIdAsync(
                        order.UserId.Value,
                        cancellationToken);

                    userEmail = user?.Email;
                    userFullName = user != null ? $"{user.FirstName} {user.LastName}".Trim() : null;
                }

                // Get items count for this order
                var orderDetail = await _orderReadRepository.GetDetailByIdAsync(
                    order.Id,
                    cancellationToken);

                var itemsCount = orderDetail?.Items?.Count ?? 0;

                dtoItems.Add(new OrderDatatableDto
                {
                    Id = order.Id,
                    UserId = order.UserId,
                    UserEmail = userEmail,
                    UserFullName = userFullName,
                    Subtotal = order.Subtotal,
                    Tax = order.Tax,
                    ShippingCost = order.ShippingCost,
                    Total = order.Total,
                    Currency = order.Currency,
                    Status = order.Status,
                    PaymentStatus = order.PaymentStatus,
                    ShippingMethod = order.ShippingMethod,
                    TrackingNumber = order.TrackingNumber,
                    ItemsCount = itemsCount,
                    RefundedAmount = order.RefundedAmount,
                    RefundedAt = order.RefundedAt,
                    RefundReason = order.RefundReason,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = order.UpdatedAt
                });
            }

            // Create new datatable result with mapped DTOs
            var datatableResult = new DataTableResult<OrderDatatableDto>(
                result.Draw,
                result.RecordsTotal,
                result.RecordsFiltered,
                dtoItems);

            _logger.LogInformation("Retrieved {Count} orders for datatable", dtoItems.Count);
            return Result.Success(datatableResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for datatable");

            return Result.Failure<DataTableResult<OrderDatatableDto>>(
                Error.Failure(
                    code: "Orders.GetDataTableFailed",
                    message: $"Failed to retrieve orders: {ex.Message}"));
        }
    }
}
