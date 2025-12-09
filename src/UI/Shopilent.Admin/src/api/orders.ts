import apiClient, { ApiResponse } from './config';
import {
    OrderDto,
    OrderDetailDto,
    OrderDataTableDto,
    OrderDatatableRequest,
    UpdateOrderStatusRequest,
    UpdatePaymentStatusRequest,
    RefundOrderRequest,
    RefundOrderResponse,
    PartialRefundOrderRequest,
    PartialRefundOrderResponse,
    CancelOrderRequest,
    CancelOrderResponse
} from '@/models/orders';
import { DataTableResult } from '@/models/common';
import {
    OrderEndpoint,
    getOrderEndpoint,
    updateOrderStatusEndpoint,
    updatePaymentStatusEndpoint,
    refundOrderEndpoint,
    partialRefundOrderEndpoint,
    cancelOrderEndpoint,
    getOrderTrackingEndpoint,
    markOrderAsShippedEndpoint,
    markOrderAsDeliveredEndpoint
} from '@/api/endpoints';

export const orderApi = {
    // Get orders for datatable (with filtering, sorting, pagination)
    getOrdersForDatatable: (request: OrderDatatableRequest) =>
        apiClient.post<ApiResponse<DataTableResult<OrderDataTableDto>>>(OrderEndpoint.Datatable, request),

    // Get order by ID (returns OrderDetailDto with complete information)
    getOrderById: (id: string) =>
        apiClient.get<ApiResponse<OrderDetailDto>>(getOrderEndpoint(id)),

    // Update order status
    updateOrderStatus: (id: string, request: UpdateOrderStatusRequest) =>
        apiClient.put<ApiResponse<OrderDto>>(updateOrderStatusEndpoint(id), request),

    // Update payment status
    updatePaymentStatus: (id: string, request: UpdatePaymentStatusRequest) =>
        apiClient.put<ApiResponse<OrderDto>>(updatePaymentStatusEndpoint(id), request),

    // Full refund order
    refundOrder: (id: string, request: RefundOrderRequest) =>
        apiClient.post<ApiResponse<RefundOrderResponse>>(refundOrderEndpoint(id), request),

    // Partial refund order
    partialRefundOrder: (id: string, request: PartialRefundOrderRequest) =>
        apiClient.post<ApiResponse<PartialRefundOrderResponse>>(partialRefundOrderEndpoint(id), request),

    // Cancel order (POST method with reason as per OpenAPI spec)
    cancelOrder: (id: string, request?: CancelOrderRequest) =>
        apiClient.post<ApiResponse<CancelOrderResponse>>(cancelOrderEndpoint(id), request || {}),

    // Get order tracking
    getOrderTracking: (id: string) =>
        apiClient.get<ApiResponse<any>>(getOrderTrackingEndpoint(id)),

    // Mark order as shipped with tracking number
    markOrderAsShipped: (id: string, trackingNumber?: string) =>
        apiClient.put<ApiResponse<string>>(markOrderAsShippedEndpoint(id), { trackingNumber }),

    // Mark order as delivered
    markOrderAsDelivered: (id: string) =>
        apiClient.put<ApiResponse<{ id: string; status: number; updatedAt: string; message: string }>>(markOrderAsDeliveredEndpoint(id)),

    // Get recent orders for dashboard
    getRecentOrders: () =>
        apiClient.get<ApiResponse<{ orders: OrderDto[]; count: number; retrievedAt: string }>>(OrderEndpoint.GetRecent),
};