import React, {useState, useEffect} from 'react';
import {useParams, useNavigate} from 'react-router-dom';
import {useQuery, useMutation, useQueryClient} from '@tanstack/react-query';
import {ArrowLeft, Save, Package, User, CreditCard, Truck, ShoppingBag} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card';
import {FormField} from '@/components/ui/form-field';
import {Input} from '@/components/ui/input';
import {Textarea} from '@/components/ui/textarea';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select';
import {Separator} from '@/components/ui/separator';
import {Badge} from '@/components/ui/badge';
import {Table, TableBody, TableCell, TableHead, TableHeader, TableRow} from '@/components/ui/table';
import {toast} from '@/components/ui/use-toast';
import {OrderStatusBadge} from '@/components/orders/OrderStatusBadge';
import {PaymentStatusBadge} from '@/components/orders/PaymentStatusBadge';
import {PriceFormatter} from '@/components/orders/PriceFormatter';
import {useForm} from '@/hooks/useForm';
import {orderApi} from '@/api/orders';
import {
  OrderDetailDto,
  OrderStatus,
  PaymentStatus,
  UpdateOrderStatusRequest,
  UpdatePaymentStatusRequest
} from '@/models/orders';
import {useTitle} from '@/hooks/useTitle';

interface EditOrderFormData {
  userEmail: string;
  userFullName: string;
  shippingMethod: string;
  trackingNumber: string;
  status: OrderStatus;
  paymentStatus: PaymentStatus;
  refundReason?: string;
}

const EditOrderPage: React.FC = () => {
  useTitle('Edit Order');
  const {id} = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  // State for tracking what fields have changed
  const [hasChanges, setHasChanges] = useState(false);

  // Fetch order data
  const {
    data: orderData,
    isLoading: orderLoading,
    error: orderError
  } = useQuery({
    queryKey: ['order', id],
    queryFn: () => orderApi.getOrderById(id!),
    enabled: !!id,
    select: (response) => {
      console.log('Order API Response:', response.data);
      const data = response.data.data as OrderDetailDto;

      // Transform the data to add computed fields
      const transformedData = {
        ...data,
        userEmail: data.user.email,
        userFullName: `${data.user.firstName} ${data.user.lastName}`.trim(),
        itemsCount: data.items.length
      };

      return transformedData;
    }
  });

  // Update order status mutation
  const updateStatusMutation = useMutation({
    mutationFn: ({orderId, request}: { orderId: string; request: UpdateOrderStatusRequest }) =>
      orderApi.updateOrderStatus(orderId, request),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Order status updated successfully'
      });
      queryClient.invalidateQueries({queryKey: ['order', id]});
      queryClient.invalidateQueries({queryKey: ['orders']});
      setHasChanges(false);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update order status',
        variant: 'destructive'
      });
    }
  });

  // Update payment status mutation
  const updatePaymentMutation = useMutation({
    mutationFn: ({orderId, request}: { orderId: string; request: UpdatePaymentStatusRequest }) =>
      orderApi.updatePaymentStatus(orderId, request),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Payment status updated successfully'
      });
      queryClient.invalidateQueries({queryKey: ['order', id]});
      queryClient.invalidateQueries({queryKey: ['orders']});
      setHasChanges(false);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update payment status',
        variant: 'destructive'
      });
    }
  });

  // Mark order as shipped mutation
  const markShippedMutation = useMutation({
    mutationFn: ({orderId, trackingNumber}: { orderId: string; trackingNumber?: string }) =>
      orderApi.markOrderAsShipped(orderId, trackingNumber),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Order marked as shipped successfully'
      });
      queryClient.invalidateQueries({queryKey: ['order', id]});
      queryClient.invalidateQueries({queryKey: ['orders']});
      setHasChanges(false);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to mark order as shipped',
        variant: 'destructive'
      });
    }
  });

  // Form management
  const {
    values: formData,
    isSubmitting,
    handleChange,
    setValue,
    setValues,
    handleSubmit
  } = useForm<EditOrderFormData>({
    initialValues: {
      userEmail: '',
      userFullName: '',
      shippingMethod: '',
      trackingNumber: '',
      status: OrderStatus.Pending,
      paymentStatus: PaymentStatus.Pending,
      refundReason: ''
    },
    onSubmit: async (values) => {
      if (!id || !orderData) return;

      try {
        // Update order status if changed
        if (values.status !== orderData.status) {
          await updateStatusMutation.mutateAsync({
            orderId: id,
            request: {status: values.status}
          });
        }

        // Update payment status if changed
        if (values.paymentStatus !== orderData.paymentStatus) {
          await updatePaymentMutation.mutateAsync({
            orderId: id,
            request: {paymentStatus: values.paymentStatus}
          });
        }

        // Update tracking number if changed
        if (values.trackingNumber !== (orderData.trackingNumber || '')) {
          await markShippedMutation.mutateAsync({
            orderId: id,
            trackingNumber: values.trackingNumber || undefined
          });
        }

        setHasChanges(false);
      } catch (error) {
        console.log(error);
      }
    }
  });

  // Initialize form data when order is loaded
  useEffect(() => {
    if (orderData) {
      setValues({
        userEmail: orderData.userEmail || '',
        userFullName: orderData.userFullName || '',
        shippingMethod: orderData.shippingMethod || '',
        trackingNumber: orderData.trackingNumber || '',
        status: orderData.status,
        paymentStatus: orderData.paymentStatus,
        refundReason: orderData.refundReason || ''
      });
    }
  }, [orderData, setValues]);

  // Track changes to enable/disable save button
  useEffect(() => {
    if (!orderData) return;

    const hasStatusChanged = formData.status !== orderData.status;
    const hasPaymentChanged = formData.paymentStatus !== orderData.paymentStatus;
    const hasTrackingChanged = formData.trackingNumber !== (orderData.trackingNumber || '');

    setHasChanges(hasStatusChanged || hasPaymentChanged || hasTrackingChanged);
  }, [formData, orderData]);

  // Handle order status change
  const handleStatusChange = (value: string) => {
    const numericValue = parseInt(value);

    if (!isNaN(numericValue)) {
      setValue('status', numericValue as OrderStatus);
    }
  };

  // Handle payment status change
  const handlePaymentStatusChange = (value: string) => {
    if (!value || value.trim() === '') {
      return;
    }
    const numericValue = parseInt(value);

    if (!isNaN(numericValue)) {
      setValue('paymentStatus', numericValue as PaymentStatus);
    }
  };

  // Format date for display
  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  // Format address for display
  const formatAddress = (address: any) => {
    if (!address) return 'N/A';
    const parts = [address.addressLine1];
    if (address.addressLine2) parts.push(address.addressLine2);
    parts.push(`${address.city}, ${address.state} ${address.postalCode}`);
    parts.push(address.country);
    return parts.join(', ');
  };

  // Get payment status label
  const getPaymentStatusLabel = (status: number) => {
    const statusMap: Record<number, string> = {
      0: 'Pending',
      1: 'Processing',
      2: 'Completed',
      3: 'Failed',
      4: 'Cancelled',
      5: 'Refunded'
    };
    return statusMap[status] || 'Unknown';
  };

  // Get order status options
  const getOrderStatusOptions = () => {
    const options = [
      {value: OrderStatus.Pending.toString(), label: 'Pending'}, // "0"
      {value: OrderStatus.Processing.toString(), label: 'Processing'}, // "1"
      {value: OrderStatus.Shipped.toString(), label: 'Shipped'}, // "2"
      {value: OrderStatus.Delivered.toString(), label: 'Delivered'}, // "3"
      {value: OrderStatus.Returned.toString(), label: 'Returned'}, // "4"
      {value: OrderStatus.ReturnedAndRefunded.toString(), label: 'Returned & Refunded'}, // "5"
      {value: OrderStatus.Cancelled.toString(), label: 'Cancelled'} // "6"
    ];

    console.log('Order status options:', options);
    return options;
  };

  const getPaymentStatusOptions = () => {
    const options = [
      {value: PaymentStatus.Pending.toString(), label: 'Pending'}, // "0"
      {value: PaymentStatus.Processing.toString(), label: 'Processing'}, // "1"
      {value: PaymentStatus.Paid.toString(), label: 'Paid'}, // "2"
      {value: PaymentStatus.Failed.toString(), label: 'Failed'}, // "3"
      {value: PaymentStatus.Refunded.toString(), label: 'Refunded'}, // "4"
      {value: PaymentStatus.Disputed.toString(), label: 'Disputed'}, // "5"
      {value: PaymentStatus.Canceled.toString(), label: 'Canceled'} // "6"
    ];

    console.log('Payment status options:', options);
    return options;
  };

  if (orderLoading) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <Package className="size-8 animate-spin mx-auto mb-4"/>
          <p>Loading order...</p>
        </div>
      </div>
    );
  }

  if (orderError || !orderData) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <Package className="size-8 mx-auto mb-4 text-muted-foreground"/>
          <p className="text-lg font-medium">Order not found</p>
          <p className="text-muted-foreground">The order you're looking for doesn't exist.</p>
          <Button onClick={() => navigate('/orders')} className="mt-4">
            <ArrowLeft className="size-4 mr-2"/>
            Back to Orders
          </Button>
        </div>
      </div>
    );
  }

  const orderIdDisplay = orderData.id?.length > 8 ? orderData.id.slice(-8).toUpperCase() : orderData.id;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">
            Edit Order #{orderIdDisplay}
          </h1>
          <p className="text-muted-foreground">
            Manage order details and status
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => navigate('/orders')}
        >
          <ArrowLeft className="size-4 mr-2"/>
          Back to Orders
        </Button>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Left Column - Order Details */}
          <div className="lg:col-span-2 space-y-6">
            {/* Order Status Section */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center">
                  <Package className="size-5 mr-2"/>
                  Order Status
                </CardTitle>
                <CardDescription>
                  Update the current status of this order
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <FormField
                    label="Order Status"
                    htmlFor="status"
                  >
                    <Select value={formData.status.toString()} onValueChange={handleStatusChange}>
                      <SelectTrigger>
                        <SelectValue/>
                      </SelectTrigger>
                      <SelectContent>
                        {getOrderStatusOptions().map((option) => (
                          <SelectItem key={option.value} value={option.value}>
                            {option.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </FormField>

                  <FormField
                    label="Payment Status"
                    htmlFor="paymentStatus"
                  >
                    <Select value={formData.paymentStatus.toString()}
                            onValueChange={handlePaymentStatusChange}>
                      <SelectTrigger>
                        <SelectValue/>
                      </SelectTrigger>
                      <SelectContent>
                        {getPaymentStatusOptions().map((option) => (
                          <SelectItem key={option.value} value={option.value}>
                            {option.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </FormField>
                </div>

                <div className="flex items-center space-x-4 p-4 bg-muted rounded-lg">
                  <div>
                    <p className="text-sm font-medium">Current Status:</p>
                    <div className="flex items-center space-x-2 mt-1">
                      <OrderStatusBadge status={orderData.status}/>
                      <PaymentStatusBadge status={orderData.paymentStatus}/>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* Order Items */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center">
                  <ShoppingBag className="size-5 mr-2"/>
                  Order Items ({orderData.items?.length || 0})
                </CardTitle>
                <CardDescription>
                  Items in this order
                </CardDescription>
              </CardHeader>
              <CardContent>
                {orderData.items && orderData.items.length > 0 ? (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Product</TableHead>
                        <TableHead>SKU</TableHead>
                        <TableHead className="text-right">Qty</TableHead>
                        <TableHead className="text-right">Unit Price</TableHead>
                        <TableHead className="text-right">Total</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {orderData.items.map((item) => (
                        <TableRow key={item.id}>
                          <TableCell>
                            <div>
                              <p className="font-medium">{item.productData.name}</p>
                              {item.productData.variant_sku && (
                                <p className="text-sm text-muted-foreground">
                                  Variant: {item.productData.variant_sku}
                                </p>
                              )}
                            </div>
                          </TableCell>
                          <TableCell>
                            <span className="font-mono text-sm">
                              {item.productData.variant_sku || item.productData.sku || 'N/A'}
                            </span>
                          </TableCell>
                          <TableCell className="text-right">
                            {item.quantity}
                          </TableCell>
                          <TableCell className="text-right">
                            <PriceFormatter
                              amount={item.unitPrice}
                              currency={item.currency}
                            />
                          </TableCell>
                          <TableCell className="text-right">
                            <PriceFormatter
                              amount={item.totalPrice}
                              currency={item.currency}
                            />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                ) : (
                  <div className="text-center py-8 text-muted-foreground">
                    <ShoppingBag className="size-8 mx-auto mb-2"/>
                    <p>No item details available</p>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Shipping Information */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center">
                  <Truck className="size-5 mr-2"/>
                  Shipping Information
                </CardTitle>
                <CardDescription>
                  Manage shipping details and tracking
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <FormField
                    label="Shipping Method"
                    htmlFor="shippingMethod"
                  >
                    <Input
                      id="shippingMethod"
                      name="shippingMethod"
                      value={formData.shippingMethod}
                      onChange={handleChange}
                      placeholder="e.g., Standard Shipping"
                      readOnly
                      className="bg-muted"
                    />
                  </FormField>

                  <FormField
                    label="Tracking Number"
                    htmlFor="trackingNumber"
                    description="Enter the tracking number for this shipment"
                  >
                    <Input
                      id="trackingNumber"
                      name="trackingNumber"
                      value={formData.trackingNumber}
                      onChange={handleChange}
                      placeholder="e.g., 1Z999AA1234567890"
                    />
                  </FormField>
                </div>

                {orderData.shippingAddress && (
                  <FormField
                    label="Shipping Address"
                  >
                    <div className="p-3 bg-muted rounded-md text-sm">
                      <div className="font-medium">{orderData.userFullName}</div>
                      {orderData.shippingAddress.phone && (
                        <div
                          className="text-muted-foreground">{orderData.shippingAddress.phone}</div>
                      )}
                      <div className="mt-1">{formatAddress(orderData.shippingAddress)}</div>
                    </div>
                  </FormField>
                )}
              </CardContent>
            </Card>

            {/* Customer Information */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center">
                  <User className="size-5 mr-2"/>
                  Customer Information
                </CardTitle>
                <CardDescription>
                  Customer details for this order
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <FormField
                    label="Customer Name"
                    htmlFor="userFullName"
                  >
                    <Input
                      id="userFullName"
                      name="userFullName"
                      value={formData.userFullName}
                      readOnly
                      className="bg-muted"
                    />
                  </FormField>

                  <FormField
                    label="Email Address"
                    htmlFor="userEmail"
                  >
                    <Input
                      id="userEmail"
                      name="userEmail"
                      value={formData.userEmail}
                      readOnly
                      className="bg-muted"
                    />
                  </FormField>
                </div>

                {orderData.user.phone && (
                  <FormField
                    label="Phone Number"
                  >
                    <Input
                      value={orderData.user.phone}
                      readOnly
                      className="bg-muted"
                    />
                  </FormField>
                )}

                {orderData.billingAddress && (
                  <FormField
                    label="Billing Address"
                  >
                    <div className="p-3 bg-muted rounded-md text-sm">
                      <div className="font-medium">{orderData.userFullName}</div>
                      {orderData.billingAddress.phone && (
                        <div
                          className="text-muted-foreground">{orderData.billingAddress.phone}</div>
                      )}
                      <div className="mt-1">{formatAddress(orderData.billingAddress)}</div>
                    </div>
                  </FormField>
                )}
              </CardContent>
            </Card>

            {/* Payment Information */}
            {orderData.paymentMethod && (
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center">
                    <CreditCard className="size-5 mr-2"/>
                    Payment Information
                  </CardTitle>
                  <CardDescription>
                    Payment method used for this order
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <FormField
                      label="Payment Method"
                    >
                      <div className="p-3 bg-muted rounded-md text-sm">
                        {orderData.paymentMethod.displayName}
                      </div>
                    </FormField>

                    {orderData.paymentMethod.cardBrand && (
                      <FormField
                        label="Card Brand"
                      >
                        <div className="p-3 bg-muted rounded-md text-sm">
                          {orderData.paymentMethod.cardBrand}
                        </div>
                      </FormField>
                    )}
                  </div>
                </CardContent>
              </Card>
            )}

            {/* Refund Information - Only show if refunded */}
            {(orderData.refundedAmount > 0 || orderData.refundReason) && (
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center">
                    <CreditCard className="size-5 mr-2"/>
                    Refund Information
                  </CardTitle>
                  <CardDescription>
                    Details about any refunds processed
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <FormField
                      label="Refunded Amount"
                    >
                      <div className="p-3 bg-muted rounded-md">
                        <PriceFormatter
                          amount={orderData.refundedAmount}
                          currency={orderData.currency}
                        />
                      </div>
                    </FormField>

                    {orderData.refundedAt && (
                      <FormField
                        label="Refund Date"
                      >
                        <div className="p-3 bg-muted rounded-md text-sm">
                          {formatDate(orderData.refundedAt)}
                        </div>
                      </FormField>
                    )}
                  </div>

                  {orderData.refundReason && (
                    <FormField
                      label="Refund Reason"
                    >
                      <Textarea
                        value={orderData.refundReason}
                        readOnly
                        className="bg-muted min-h-[80px]"
                      />
                    </FormField>
                  )}
                </CardContent>
              </Card>
            )}
          </div>

          {/* Right Column - Order Summary */}
          <div className="space-y-6">
            {/* Order Summary */}
            <Card>
              <CardHeader>
                <CardTitle>Order Summary</CardTitle>
                <CardDescription>
                  Order #{orderIdDisplay}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-3">
                  <div className="flex justify-between text-sm">
                                        <span
                                          className="text-muted-foreground">Items ({orderData.items?.length || 0})</span>
                    <PriceFormatter
                      amount={orderData.subtotal}
                      currency={orderData.currency}
                    />
                  </div>

                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Tax</span>
                    <PriceFormatter
                      amount={orderData.tax}
                      currency={orderData.currency}
                    />
                  </div>

                  <div className="flex justify-between text-sm">
                    <span className="text-muted-foreground">Shipping</span>
                    <PriceFormatter
                      amount={orderData.shippingCost}
                      currency={orderData.currency}
                    />
                  </div>

                  <Separator/>

                  <div className="flex justify-between font-medium">
                    <span>Total</span>
                    <PriceFormatter
                      amount={orderData.total}
                      currency={orderData.currency}
                    />
                  </div>

                  {orderData.refundedAmount > 0 && (
                    <>
                      <div className="flex justify-between text-sm text-destructive">
                        <span>Refunded</span>
                        <span>-<PriceFormatter
                          amount={orderData.refundedAmount}
                          currency={orderData.currency}
                        /></span>
                      </div>
                      <Separator/>
                      <div className="flex justify-between font-medium">
                        <span>Net Amount</span>
                        <PriceFormatter
                          amount={orderData.total - orderData.refundedAmount}
                          currency={orderData.currency}
                        />
                      </div>
                    </>
                  )}
                </div>
              </CardContent>
            </Card>

            {/* Order Timeline */}
            <Card>
              <CardHeader>
                <CardTitle>Order Timeline</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div>
                  <p className="text-sm font-medium">Created</p>
                  <p className="text-sm text-muted-foreground">
                    {formatDate(orderData.createdAt)}
                  </p>
                </div>

                <div>
                  <p className="text-sm font-medium">Last Updated</p>
                  <p className="text-sm text-muted-foreground">
                    {formatDate(orderData.updatedAt)}
                  </p>
                </div>

                {orderData.refundedAt && (
                  <div>
                    <p className="text-sm font-medium">Refunded</p>
                    <p className="text-sm text-muted-foreground">
                      {formatDate(orderData.refundedAt)}
                    </p>
                  </div>
                )}

                {orderData.lastModified && (
                  <div>
                    <p className="text-sm font-medium">Last Modified</p>
                    <p className="text-sm text-muted-foreground">
                      {formatDate(orderData.lastModified)}
                    </p>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Payment History */}
            {orderData.payments && orderData.payments.length > 0 && (
              <Card>
                <CardHeader>
                  <CardTitle>Payment History</CardTitle>
                  <CardDescription>
                    Payment transactions for this order
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <div className="space-y-3">
                    {orderData.payments.map((payment) => (
                      <div key={payment.id}
                           className="flex justify-between items-center p-3 bg-muted rounded-lg">
                        <div>
                          <p className="text-sm font-medium">Payment</p>
                          <p className="text-xs text-muted-foreground">
                            {formatDate(payment.createdAt)}
                          </p>
                          {payment.transactionId && (
                            <p className="text-xs text-muted-foreground font-mono">
                              {payment.transactionId}
                            </p>
                          )}
                          {payment.externalReference && (
                            <p className="text-xs text-muted-foreground font-mono">
                              Ref: {payment.externalReference}
                            </p>
                          )}
                        </div>
                        <div className="text-right">
                          <PriceFormatter
                            amount={payment.amount}
                            currency={payment.currency}
                          />
                          <Badge
                            variant={payment.status === 2 ? 'default' : 'secondary'}
                            className="ml-2 text-xs"
                          >
                            {getPaymentStatusLabel(payment.status)}
                          </Badge>
                        </div>
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>
            )}

            {/* Customer Details */}
            <Card>
              <CardHeader>
                <CardTitle>Customer Details</CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div>
                  <p className="text-sm font-medium">Customer ID</p>
                  <p className="text-sm text-muted-foreground font-mono">
                    {orderData.user.id.slice(-8).toUpperCase()}
                  </p>
                </div>

                <div>
                  <p className="text-sm font-medium">Member Since</p>
                  <p className="text-sm text-muted-foreground">
                    {formatDate(orderData.user.createdAt)}
                  </p>
                </div>

                <div>
                  <p className="text-sm font-medium">Email Verified</p>
                  <Badge variant={orderData.user.emailVerified ? 'default' : 'secondary'}>
                    {orderData.user.emailVerified ? 'Verified' : 'Not Verified'}
                  </Badge>
                </div>

                <div>
                  <p className="text-sm font-medium">Account Status</p>
                  <Badge variant={orderData.user.isActive ? 'default' : 'destructive'}>
                    {orderData.user.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </div>
              </CardContent>
            </Card>

            {/* Save Button */}
            <Button
              type="submit"
              disabled={!hasChanges || isSubmitting}
              className="w-full"
              size="lg"
            >
              <Save className="size-4 mr-2"/>
              {isSubmitting ? 'Saving...' : 'Save Changes'}
            </Button>
          </div>
        </div>
      </form>
    </div>
  );
};

export default EditOrderPage;
