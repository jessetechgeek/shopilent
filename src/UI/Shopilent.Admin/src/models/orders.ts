import {DataTableRequest} from './common';

// Order Status Enums (based on API schema)
export enum OrderStatus {
  Pending = 0,
  Processing = 1,
  Shipped = 2,
  Delivered = 3,
  Cancelled = 4,
  Refunded = 5
}

export enum PaymentStatus {
  Pending = 0,
  Processing = 1,
  Paid = 2,
  Failed = 3,
  Refunded = 4,
  Disputed = 5,
  Canceled = 6,
  PartiallyRefunded = 7
}

// Extended Order Detail DTO (updated based on actual API response)
export interface OrderDetailDto extends OrderDto {
  items: OrderItemDto[];
  user: UserDto;
  paymentMethod: PaymentMethodDto;
  payments: PaymentDto[];
  billingAddressId: string;
  shippingAddressId: string;
  paymentMethodId: string;
  shippingAddress: AddressDto;
  billingAddress: AddressDto;
  metadata?: Record<string, any>;
  createdBy?: string;
  modifiedBy?: string;
  lastModified?: string;
}

// Basic Order DTO (updated to match API response structure)
export interface OrderDto {
  id: string;
  userId: string;
  userEmail: string; // This is derived from user.email
  userFullName: string; // This is derived from user.firstName + lastName
  subtotal: number;
  tax: number;
  shippingCost: number;
  total: number;
  currency: string;
  status: OrderStatus;
  paymentStatus: PaymentStatus;
  shippingMethod: string;
  trackingNumber?: string;
  itemsCount: number; // This needs to be calculated from items.length
  refundedAmount: number;
  refundedAt?: string;
  refundReason?: string;
  createdAt: string;
  updatedAt: string;
}

// Address DTO (updated based on actual API response)
export interface AddressDto {
  id: string;
  userId: string;
  addressLine1: string;
  addressLine2?: string;
  city: string;
  state: string;
  postalCode: string;
  country: string;
  phone?: string;
  isDefault: boolean;
  addressType: number;
  createdAt: string;
  updatedAt: string;
}

// Order Item DTO (updated based on actual API response)
export interface OrderItemDto {
  id: string;
  orderId: string;
  productId: string;
  variantId?: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number; // Note: API uses 'totalPrice' not 'total'
  currency: string;
  productData: {
    sku: string;
    name: string;
    slug: string;
    variant_sku?: string;
  };
  createdAt: string;
  updatedAt: string;
}

// User DTO (updated based on actual API response)
export interface UserDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  middleName?: string;
  phone?: string;
  role: number;
  isActive: boolean;
  lastLogin?: string;
  emailVerified: boolean;
  createdAt: string;
  updatedAt: string;
}

// Payment Method DTO (updated based on actual API response)
export interface PaymentMethodDto {
  id: string;
  userId: string;
  type: number;
  provider: number;
  displayName: string;
  cardBrand?: string;
  lastFourDigits?: string;
  expiryDate?: string;
  isDefault: boolean;
  isActive: boolean;
  metadata?: Record<string, any>;
  createdAt: string;
  updatedAt: string;
}

// Payment DTO (updated based on actual API response)
export interface PaymentDto {
  id: string;
  orderId: string;
  userId: string;
  amount: number;
  currency: string;
  methodType: number;
  provider: number;
  status: number;
  externalReference?: string;
  transactionId?: string;
  paymentMethodId?: string;
  processedAt?: string;
  errorMessage?: string;
  metadata?: Record<string, any>;
  createdAt: string;
  updatedAt: string;
}

export interface OrderDataTableDto extends OrderDto {
  // All fields already included in OrderDto based on API schema
}

// Order Requests
export interface OrderDatatableRequest extends DataTableRequest {
}

export interface UpdateOrderStatusRequest {
  status: OrderStatus;
  reason?: string;
}

export interface UpdatePaymentStatusRequest {
  paymentStatus: PaymentStatus;
}

// Full refund request (based on OpenAPI ProcessOrderRefundRequestV1)
export interface RefundOrderRequest {
  reason?: string;
}

// Partial refund request (based on OpenAPI ProcessOrderPartialRefundRequestV1)
export interface PartialRefundOrderRequest {
  amount: number;
  currency: string;
  reason?: string;
}

// Full refund response (based on OpenAPI ProcessOrderRefundResponseV1)
export interface RefundOrderResponse {
  orderId: string;
  refundAmount: number;
  currency: string;
  totalRefundedAmount: number;
  reason?: string;
  refundedAt: string;
  isFullyRefunded: boolean;
}

// Partial refund response (based on OpenAPI ProcessOrderPartialRefundResponseV1)
export interface PartialRefundOrderResponse {
  orderId: string;
  refundAmount: number;
  currency: string;
  reason?: string;
  refundedAt: string;
  status: string;
}

// Cancel Order Request (based on OpenAPI spec)
export interface CancelOrderRequest {
  reason?: string;
}

// Cancel Order Response
export interface CancelOrderResponse {
  orderId: string;
  status: OrderStatus;
  reason?: string;
  cancelledAt: string;
}
