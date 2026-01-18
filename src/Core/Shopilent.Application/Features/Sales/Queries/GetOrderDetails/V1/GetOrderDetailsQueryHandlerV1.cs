using Microsoft.Extensions.Logging;
using Shopilent.Application.Abstractions.Messaging;
using Shopilent.Application.Abstractions.S3Storage;
using Shopilent.Domain.Common.Errors;
using Shopilent.Domain.Common.Results;
using Shopilent.Domain.Sales.DTOs;
using Shopilent.Domain.Sales.Errors;
using Shopilent.Domain.Sales.Repositories.Read;

namespace Shopilent.Application.Features.Sales.Queries.GetOrderDetails.V1;

internal sealed class GetOrderDetailsQueryHandlerV1 : IQueryHandler<GetOrderDetailsQueryV1, OrderDetailDto>
{
    private readonly IOrderReadRepository _orderReadRepository;
    private readonly ILogger<GetOrderDetailsQueryHandlerV1> _logger;
    private readonly IS3StorageService _s3StorageService;

    public GetOrderDetailsQueryHandlerV1(
        IOrderReadRepository orderReadRepository,
        ILogger<GetOrderDetailsQueryHandlerV1> logger,
        IS3StorageService s3StorageService)
    {
        _orderReadRepository = orderReadRepository;
        _logger = logger;
        _s3StorageService = s3StorageService;
    }

    public async Task<Result<OrderDetailDto>> Handle(GetOrderDetailsQueryV1 request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Retrieving order details for OrderId: {OrderId}, UserId: {UserId}, IsAdmin: {IsAdmin}, IsManager: {IsManager}",
                request.OrderId, request.CurrentUserId, request.IsAdmin, request.IsManager);

            // Get the order details
            var orderDetails = await _orderReadRepository.GetDetailByIdAsync(request.OrderId, cancellationToken);

            if (orderDetails == null)
            {
                _logger.LogWarning("Order with ID {OrderId} was not found", request.OrderId);
                return Result.Failure<OrderDetailDto>(OrderErrors.NotFound(request.OrderId));
            }

            // Authorization check: Customers can only see their own orders, Admins/Managers can see all
            if (!IsAuthorizedToViewOrder(orderDetails, request.CurrentUserId, request.IsAdmin, request.IsManager))
            {
                _logger.LogWarning("User {UserId} attempted to access order {OrderId} belonging to user {OrderUserId}",
                    request.CurrentUserId, request.OrderId, orderDetails.UserId);

                return Result.Failure<OrderDetailDto>(
                    Error.Forbidden("Order.AccessDenied", "You are not authorized to view this order"));
            }

            // Transform image keys to presigned URLs for order items
            await TransformOrderItemImagesAsync(orderDetails, cancellationToken);

            _logger.LogInformation("Successfully retrieved order details for OrderId: {OrderId}", request.OrderId);
            return Result.Success(orderDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order details for OrderId: {OrderId}", request.OrderId);

            return Result.Failure<OrderDetailDto>(
                Error.Failure(
                    code: "Order.GetDetailsFailed",
                    message: $"Failed to retrieve order details: {ex.Message}"));
        }
    }

    private static bool IsAuthorizedToViewOrder(OrderDetailDto order, Guid? currentUserId, bool isAdmin, bool isManager)
    {
        // If no user context, deny access
        if (!currentUserId.HasValue)
            return false;

        // Admins and Managers can view all orders
        if (isAdmin || isManager)
            return true;

        // Regular users (customers) can only view their own orders
        return order.UserId == currentUserId;
    }

    private async Task TransformOrderItemImagesAsync(OrderDetailDto order, CancellationToken cancellationToken)
    {
        foreach (var item in order.Items)
        {
            // Extract thumbnail_key from productData
            if (item.ProductData != null && item.ProductData.ContainsKey("thumbnail_key"))
            {
                var thumbnailKey = item.ProductData["thumbnail_key"]?.ToString();

                if (!string.IsNullOrEmpty(thumbnailKey))
                {
                    var imageUrlResult = await _s3StorageService.GetPublicUrlAsync(thumbnailKey, cancellationToken);

                    if (imageUrlResult.IsSuccess)
                    {
                        item.ImageUrl = imageUrlResult.Value;
                    }
                }

                // Remove image keys from productData as they're now in ImageUrl
                item.ProductData.Remove("image_key");
                item.ProductData.Remove("thumbnail_key");
            }
        }
    }
}
