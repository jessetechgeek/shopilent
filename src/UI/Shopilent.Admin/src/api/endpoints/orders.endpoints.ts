/**
 * Order API Endpoints
 */

import { replacePath } from './types';

/**
 * Order endpoints
 */
export enum OrderEndpoint {
  // CRUD
  GetById = '/v1/orders/{id}',

  // Queries
  Datatable = '/v1/orders/datatable',
  GetRecent = '/v1/orders/recent',

  // Status updates
  UpdateStatus = '/v1/orders/{id}/status',
  UpdatePaymentStatus = '/v1/orders/{id}/payment-status',

  // Order actions
  Refund = '/v1/orders/{id}/refund',
  PartialRefund = '/v1/orders/{id}/partial-refund',
  Cancel = '/v1/orders/{id}/cancel',
  MarkShipped = '/v1/orders/{id}/shipped',
  MarkDelivered = '/v1/orders/{id}/delivered',
  MarkReturned = '/v1/orders/{id}/return',

  // Tracking
  GetTracking = '/v1/orders/{id}/tracking',
}

/**
 * Get order endpoint by ID
 *
 * @param id - Order ID
 * @returns Order endpoint with ID
 */
export function getOrderEndpoint(id: string): string {
  return replacePath(OrderEndpoint.GetById, { id });
}

/**
 * Get update order status endpoint by ID
 *
 * @param id - Order ID
 * @returns Update order status endpoint with ID
 */
export function updateOrderStatusEndpoint(id: string): string {
  return replacePath(OrderEndpoint.UpdateStatus, { id });
}

/**
 * Get update payment status endpoint by ID
 *
 * @param id - Order ID
 * @returns Update payment status endpoint with ID
 */
export function updatePaymentStatusEndpoint(id: string): string {
  return replacePath(OrderEndpoint.UpdatePaymentStatus, { id });
}

/**
 * Get refund order endpoint by ID
 *
 * @param id - Order ID
 * @returns Refund order endpoint with ID
 */
export function refundOrderEndpoint(id: string): string {
  return replacePath(OrderEndpoint.Refund, { id });
}

/**
 * Get partial refund order endpoint by ID
 *
 * @param id - Order ID
 * @returns Partial refund order endpoint with ID
 */
export function partialRefundOrderEndpoint(id: string): string {
  return replacePath(OrderEndpoint.PartialRefund, { id });
}

/**
 * Get cancel order endpoint by ID
 *
 * @param id - Order ID
 * @returns Cancel order endpoint with ID
 */
export function cancelOrderEndpoint(id: string): string {
  return replacePath(OrderEndpoint.Cancel, { id });
}

/**
 * Get order tracking endpoint by ID
 *
 * @param id - Order ID
 * @returns Order tracking endpoint with ID
 */
export function getOrderTrackingEndpoint(id: string): string {
  return replacePath(OrderEndpoint.GetTracking, { id });
}

/**
 * Get mark order as shipped endpoint by ID
 *
 * @param id - Order ID
 * @returns Mark order as shipped endpoint with ID
 */
export function markOrderAsShippedEndpoint(id: string): string {
  return replacePath(OrderEndpoint.MarkShipped, { id });
}

/**
 * Get mark order as delivered endpoint by ID
 *
 * @param id - Order ID
 * @returns Mark order as delivered endpoint with ID
 */
export function markOrderAsDeliveredEndpoint(id: string): string {
  return replacePath(OrderEndpoint.MarkDelivered, { id });
}

/**
 * Get mark order as returned endpoint by ID
 *
 * @param id - Order ID
 * @returns Mark order as returned endpoint with ID
 */
export function markOrderAsReturnedEndpoint(id: string): string {
  return replacePath(OrderEndpoint.MarkReturned, { id });
}
