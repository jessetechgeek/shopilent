import React, {useState} from 'react';
import {useQuery, useMutation, useQueryClient} from '@tanstack/react-query';
import {Loader2, ShoppingCart} from 'lucide-react';
import {orderApi} from '@/api/orders';
import {OrderTable} from '@/components/orders/OrderTable';
import {CancelOrderDialog} from '@/components/orders/CancelOrderDialog';
import {UpdateOrderStatusDialog} from '@/components/orders/UpdateOrderStatusDialog';
import {UpdateTrackingDialog} from '@/components/orders/UpdateTrackingDialog';
import {MarkAsShippedDialog} from '@/components/orders/MarkAsShippedDialog';
import {MarkAsDeliveredDialog} from '@/components/orders/MarkAsDeliveredDialog';
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogCancel,
  AlertDialogAction,
} from '@/components/ui/alert-dialog';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {toast} from '@/components/ui/use-toast';
import {
  OrderDto,
  OrderDatatableRequest,
  RefundOrderRequest,
  PartialRefundOrderRequest,
  CancelOrderRequest,
  OrderStatus,
  UpdateOrderStatusRequest,
  MarkAsReturnedRequest
} from '@/models/orders';
import {useTitle} from '@/hooks/useTitle';

const OrdersPage: React.FC = () => {
  useTitle('Orders');
  const queryClient = useQueryClient();

  // State for table
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const [sortColumn, setSortColumn] = useState(7); // createdAt column by default
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc');

  // State for dialogs
  const [cancelDialogOpen, setCancelDialogOpen] = useState(false);
  const [refundDialogOpen, setRefundDialogOpen] = useState(false);
  const [partialRefundDialogOpen, setPartialRefundDialogOpen] = useState(false);
  const [detailsDialogOpen, setDetailsDialogOpen] = useState(false);
  const [statusDialogOpen, setStatusDialogOpen] = useState(false);
  const [paymentDialogOpen, setPaymentDialogOpen] = useState(false);
  const [trackingDialogOpen, setTrackingDialogOpen] = useState(false);
  const [markAsShippedDialogOpen, setMarkAsShippedDialogOpen] = useState(false);
  const [markAsDeliveredDialogOpen, setMarkAsDeliveredDialogOpen] = useState(false);
  const [markAsReturnedDialogOpen, setMarkAsReturnedDialogOpen] = useState(false);
  const [selectedOrder, setSelectedOrder] = useState<OrderDto | null>(null);

  // Prepare datatables request
  const datatableRequest: OrderDatatableRequest = {
    draw: 1,
    start: pageIndex * pageSize,
    length: pageSize,
    search: {
      value: searchQuery,
      regex: false
    },
    columns: [
      {data: 'id', name: 'id', searchable: true, orderable: true, search: {value: '', regex: false}},
      {
        data: 'userFullName',
        name: 'userFullName',
        searchable: true,
        orderable: true,
        search: {value: '', regex: false}
      },
      {data: 'userEmail', name: 'userEmail', searchable: true, orderable: true, search: {value: '', regex: false}},
      {data: 'total', name: 'total', searchable: false, orderable: true, search: {value: '', regex: false}},
      {data: 'status', name: 'status', searchable: false, orderable: true, search: {value: '', regex: false}},
      {
        data: 'paymentStatus',
        name: 'paymentStatus',
        searchable: false,
        orderable: true,
        search: {value: '', regex: false}
      },
      {data: 'itemsCount', name: 'itemsCount', searchable: false, orderable: true, search: {value: '', regex: false}},
      {data: 'createdAt', name: 'createdAt', searchable: false, orderable: true, search: {value: '', regex: false}}
    ],
    order: [
      {column: sortColumn, dir: sortDirection}
    ]
  };

  // Query orders
  const {data, isLoading, error} = useQuery({
    queryKey: ['orders', pageIndex, pageSize, searchQuery, sortColumn, sortDirection],
    queryFn: async () => {
      const response = await orderApi.getOrdersForDatatable(datatableRequest);
      if (response.data.succeeded) {
        return response.data.data;
      }
      throw new Error(response.data.message || 'Failed to fetch orders');
    }
  });

  // Handle query errors with useEffect to avoid setState during render
  React.useEffect(() => {
    if (error) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to fetch orders',
        variant: 'destructive'
      });
    }
  }, [error]);

  // Cancel order mutation with reason support
  const cancelOrderMutation = useMutation({
    mutationFn: ({id, reason}: { id: string; reason?: string }) => {
      const request: CancelOrderRequest = reason ? {reason} : {};
      return orderApi.cancelOrder(id, request);
    },
    onSuccess: (_, variables) => {
      toast({
        title: 'Success',
        description: `Order cancelled successfully${variables.reason ? ` (Reason: ${variables.reason})` : ''}`,
        variant: 'default'
      });
      queryClient.invalidateQueries({queryKey: ['orders']});
      setCancelDialogOpen(false);
      setSelectedOrder(null);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to cancel order',
        variant: 'destructive'
      });
    }
  });

  // Update order status mutation
  const updateStatusMutation = useMutation({
    mutationFn: ({id, request}: { id: string; request: UpdateOrderStatusRequest }) =>
      orderApi.updateOrderStatus(id, request),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Order status updated successfully'
      });
      queryClient.invalidateQueries({queryKey: ['orders']});
      setStatusDialogOpen(false);
      setSelectedOrder(null);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update order status',
        variant: 'destructive'
      });
    }
  });

  // Mark order as shipped mutation
  const markShippedMutation = useMutation({
    mutationFn: ({id, trackingNumber}: { id: string; trackingNumber?: string }) =>
      orderApi.markOrderAsShipped(id, trackingNumber),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Order marked as shipped successfully'
      });
      queryClient.invalidateQueries({queryKey: ['orders']});
      setTrackingDialogOpen(false);
      setMarkAsShippedDialogOpen(false);
      setSelectedOrder(null);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update tracking information',
        variant: 'destructive'
      });
    }
  });

  // Mark order as delivered mutation
  const markDeliveredMutation = useMutation({
    mutationFn: (id: string) => orderApi.markOrderAsDelivered(id),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Order marked as delivered successfully'
      });
      queryClient.invalidateQueries({queryKey: ['orders']});
      setMarkAsDeliveredDialogOpen(false);
      setSelectedOrder(null);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to mark order as delivered',
        variant: 'destructive'
      });
    }
  });

  // Mark order as returned mutation
  const markReturnedMutation = useMutation({
    mutationFn: ({id, request}: { id: string; request: MarkAsReturnedRequest }) =>
      orderApi.markOrderAsReturned(id, request),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Order marked as returned successfully'
      });
      queryClient.invalidateQueries({queryKey: ['orders']});
      setMarkAsReturnedDialogOpen(false);
      setSelectedOrder(null);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to mark order as returned',
        variant: 'destructive'
      });
    }
  });

  // Full refund order mutation
  const refundOrderMutation = useMutation({
    mutationFn: ({id, request}: { id: string; request: RefundOrderRequest }) =>
      orderApi.refundOrder(id, request),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Order refunded successfully',
        variant: 'default'
      });
      queryClient.invalidateQueries({queryKey: ['orders']});
      setRefundDialogOpen(false);
      setSelectedOrder(null);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to process refund',
        variant: 'destructive'
      });
    }
  });

  // Partial refund order mutation
  const partialRefundOrderMutation = useMutation({
    mutationFn: ({id, request}: { id: string; request: PartialRefundOrderRequest }) =>
      orderApi.partialRefundOrder(id, request),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Partial refund processed successfully',
        variant: 'default'
      });
      queryClient.invalidateQueries({queryKey: ['orders']});
      setPartialRefundDialogOpen(false);
      setSelectedOrder(null);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to process partial refund',
        variant: 'destructive'
      });
    }
  });

  // Handle sorting
  const handleSort = (column: number, direction: 'asc' | 'desc') => {
    setSortColumn(column);
    setSortDirection(direction);
  };

  // Handle view details
  const handleViewDetails = (order: OrderDto) => {
    setSelectedOrder(order);
    setDetailsDialogOpen(true);
  };

  // Handle update status
  const handleUpdateStatus = (order: OrderDto) => {
    setSelectedOrder(order);
    setStatusDialogOpen(true);
  };

  // Handle update payment
  const handleUpdatePayment = (order: OrderDto) => {
    setSelectedOrder(order);
    setPaymentDialogOpen(true);
  };

  // Handle update tracking
  const handleUpdateTracking = (order: OrderDto) => {
    setSelectedOrder(order);
    setTrackingDialogOpen(true);
  };

  // Handle refund
  const handleRefund = (order: OrderDto) => {
    setSelectedOrder(order);
    setRefundDialogOpen(true);
  };

  // Handle partial refund
  const handlePartialRefund = (order: OrderDto) => {
    setSelectedOrder(order);
    setPartialRefundDialogOpen(true);
  };

  // Handle mark as shipped
  const handleMarkAsShipped = (order: OrderDto) => {
    setSelectedOrder(order);
    setMarkAsShippedDialogOpen(true);
  };

  // Handle mark as delivered
  const handleMarkAsDelivered = (order: OrderDto) => {
    setSelectedOrder(order);
    setMarkAsDeliveredDialogOpen(true);
  };

  // Handle mark as returned
  const handleMarkAsReturned = (order: OrderDto) => {
    setSelectedOrder(order);
    setMarkAsReturnedDialogOpen(true);
  };

  // Handle cancel
  const handleCancel = (order: OrderDto) => {
    setSelectedOrder(order);
    setCancelDialogOpen(true);
  };

  // Handle confirm cancel with reason
  const handleConfirmCancel = (reason?: string) => {
    if (selectedOrder) {
      cancelOrderMutation.mutate({
        id: selectedOrder.id,
        reason
      });
    }
  };

  // Handle confirm status update
  const handleConfirmStatusUpdate = (status: OrderStatus, reason?: string) => {
    if (selectedOrder) {
      const request: UpdateOrderStatusRequest = {status, reason};
      updateStatusMutation.mutate({id: selectedOrder.id, request});
    }
  };

  // Handle confirm tracking update
  const handleConfirmTrackingUpdate = (trackingNumber: string) => {
    if (selectedOrder) {
      markShippedMutation.mutate({
        id: selectedOrder.id,
        trackingNumber: trackingNumber || undefined
      });
    }
  };

  // Handle confirm mark as shipped
  const handleConfirmMarkAsShipped = (trackingNumber?: string) => {
    if (selectedOrder) {
      markShippedMutation.mutate({
        id: selectedOrder.id,
        trackingNumber
      });
    }
  };

  // Handle confirm mark as delivered
  const handleConfirmMarkAsDelivered = () => {
    if (selectedOrder) {
      markDeliveredMutation.mutate(selectedOrder.id);
    }
  };

  // Handle confirm mark as returned
  const handleConfirmMarkAsReturned = (returnReason?: string) => {
    if (selectedOrder) {
      const request: MarkAsReturnedRequest = {
        returnReason
      };
      markReturnedMutation.mutate({id: selectedOrder.id, request});
    }
  };

  // Handle confirm refund
  const handleConfirmRefund = (reason?: string) => {
    if (selectedOrder) {
      const refundRequest: RefundOrderRequest = {
        reason: reason || 'Customer request'
      };
      refundOrderMutation.mutate({id: selectedOrder.id, request: refundRequest});
    }
  };

  // Handle confirm partial refund
  const handleConfirmPartialRefund = (amount: number, reason?: string) => {
    if (selectedOrder) {
      const refundRequest: PartialRefundOrderRequest = {
        amount,
        currency: selectedOrder.currency,
        reason: reason || 'Customer request'
      };
      partialRefundOrderMutation.mutate({id: selectedOrder.id, request: refundRequest});
    }
  };

  // Show loading state
  if (isLoading && !data) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <Loader2 className="h-8 w-8 animate-spin mx-auto mb-4"/>
          <p>Loading orders...</p>
        </div>
      </div>
    );
  }

  // Show error state
  if (error && !data) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <ShoppingCart className="h-8 w-8 mx-auto mb-4 text-muted-foreground"/>
          <p className="text-lg font-medium">Error loading orders</p>
          <p className="text-muted-foreground">Please try refreshing the page.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Orders</h1>
          <p className="text-muted-foreground">
            Manage customer orders and track fulfillment
          </p>
        </div>
        <div className="flex items-center space-x-2">
          <ShoppingCart className="h-5 w-5 text-muted-foreground"/>
          <span className="text-sm text-muted-foreground">
                        {data?.recordsTotal || 0} total orders
                    </span>
        </div>
      </div>

      {/* Orders Table */}
      <OrderTable
        data={data?.data || []}
        totalRecords={data?.recordsTotal || 0}
        filteredRecords={data?.recordsFiltered || 0}
        pageIndex={pageIndex}
        pageSize={pageSize}
        sortColumn={sortColumn}
        sortDirection={sortDirection}
        searchQuery={searchQuery}
        onPageChange={setPageIndex}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setPageIndex(0);
        }}
        onSearch={(query) => {
          setSearchQuery(query);
          setPageIndex(0);
        }}
        onSort={handleSort}
        onViewDetails={handleViewDetails}
        onUpdateStatus={handleUpdateStatus}
        onUpdatePayment={handleUpdatePayment}
        onUpdateTracking={handleUpdateTracking}
        onMarkAsShipped={handleMarkAsShipped}
        onMarkAsDelivered={handleMarkAsDelivered}
        onMarkAsReturned={handleMarkAsReturned}
        onRefund={handleRefund}
        onPartialRefund={handlePartialRefund}
        onCancel={handleCancel}
      />

      {/* Enhanced Cancel Order Dialog */}
      <CancelOrderDialog
        order={selectedOrder}
        open={cancelDialogOpen}
        onOpenChange={setCancelDialogOpen}
        onConfirm={handleConfirmCancel}
        isLoading={cancelOrderMutation.isPending}
      />

      {/* Refund Order Confirmation Dialog */}
      <AlertDialog open={refundDialogOpen} onOpenChange={setRefundDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Process Full Refund</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to process a full refund for order #{selectedOrder?.id.slice(-8).toUpperCase()}?
              This will refund the full amount of {selectedOrder && (
              <span className="font-semibold">
                                {new Intl.NumberFormat('en-US', {
                                  style: 'currency',
                                  currency: selectedOrder.currency,
                                }).format(selectedOrder.total)}
                            </span>
            )}.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => handleConfirmRefund()}
              className="bg-orange-600 text-white hover:bg-orange-700"
            >
              {refundOrderMutation.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin"/>
                  Processing...
                </>
              ) : (
                'Process Full Refund'
              )}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Partial Refund Order Dialog */}
      {partialRefundDialogOpen && (
        <Dialog open={partialRefundDialogOpen} onOpenChange={setPartialRefundDialogOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Process Partial Refund</DialogTitle>
            </DialogHeader>
            <div className="space-y-4">
              <p className="text-sm text-muted-foreground">
                Order #{selectedOrder?.id.slice(-8).toUpperCase()} -
                Total: {selectedOrder && new Intl.NumberFormat('en-US', {
                style: 'currency',
                currency: selectedOrder.currency,
              }).format(selectedOrder.total)}
              </p>
              <p className="text-sm text-muted-foreground">
                Partial refund dialog with amount input coming soon...
              </p>
              <div className="flex justify-end space-x-2">
                <button
                  onClick={() => setPartialRefundDialogOpen(false)}
                  className="px-4 py-2 text-sm border rounded-md hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  onClick={() => handleConfirmPartialRefund(selectedOrder?.total || 0)}
                  disabled={partialRefundOrderMutation.isPending}
                  className="px-4 py-2 text-sm bg-yellow-600 text-white rounded-md hover:bg-yellow-700 disabled:opacity-50"
                >
                  {partialRefundOrderMutation.isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin"/>
                      Processing...
                    </>
                  ) : (
                    'Process Partial Refund'
                  )}
                </button>
              </div>
            </div>
          </DialogContent>
        </Dialog>
      )}

      {/* Order Details Dialog - Placeholder */}
      {detailsDialogOpen && (
        <Dialog open={detailsDialogOpen} onOpenChange={setDetailsDialogOpen}>
          <DialogContent className="max-w-4xl">
            <DialogHeader>
              <DialogTitle>
                Order Details - #{selectedOrder?.id.slice(-8).toUpperCase()}
              </DialogTitle>
            </DialogHeader>
            <div className="space-y-4">
              {selectedOrder && (
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <h4 className="font-medium">Customer Information</h4>
                    <p className="text-sm text-muted-foreground">
                      {selectedOrder.userFullName}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      {selectedOrder.userEmail}
                    </p>
                  </div>
                  <div>
                    <h4 className="font-medium">Order Summary</h4>
                    <p className="text-sm text-muted-foreground">
                      Total: {new Intl.NumberFormat('en-US', {
                      style: 'currency',
                      currency: selectedOrder.currency,
                    }).format(selectedOrder.total)}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      Items: {selectedOrder.itemsCount}
                    </p>
                  </div>
                </div>
              )}
              <p className="text-sm text-muted-foreground">
                Complete order details implementation coming soon...
              </p>
            </div>
          </DialogContent>
        </Dialog>
      )}

      {/* Update Order Status Dialog */}
      <UpdateOrderStatusDialog
        order={selectedOrder}
        open={statusDialogOpen}
        onOpenChange={setStatusDialogOpen}
        onConfirm={handleConfirmStatusUpdate}
        isLoading={updateStatusMutation.isPending}
      />

      {/* Payment Update Dialog - Placeholder */}
      {paymentDialogOpen && (
        <Dialog open={paymentDialogOpen} onOpenChange={setPaymentDialogOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Update Payment Status</DialogTitle>
            </DialogHeader>
            <p className="text-sm text-muted-foreground">
              Payment status update dialog implementation coming soon...
            </p>
          </DialogContent>
        </Dialog>
      )}

      {/* Update Tracking Dialog */}
      <UpdateTrackingDialog
        order={selectedOrder}
        open={trackingDialogOpen}
        onOpenChange={setTrackingDialogOpen}
        onConfirm={handleConfirmTrackingUpdate}
        isLoading={markShippedMutation.isPending}
      />

      {/* Mark as Shipped Dialog */}
      <MarkAsShippedDialog
        order={selectedOrder}
        open={markAsShippedDialogOpen}
        onOpenChange={setMarkAsShippedDialogOpen}
        onConfirm={handleConfirmMarkAsShipped}
        isLoading={markShippedMutation.isPending}
      />

      {/* Mark as Delivered Dialog */}
      <MarkAsDeliveredDialog
        order={selectedOrder}
        open={markAsDeliveredDialogOpen}
        onOpenChange={setMarkAsDeliveredDialogOpen}
        onConfirm={handleConfirmMarkAsDelivered}
        isLoading={markDeliveredMutation.isPending}
      />

      {/* Mark as Returned Dialog */}
      {markAsReturnedDialogOpen && (
        <Dialog open={markAsReturnedDialogOpen} onOpenChange={setMarkAsReturnedDialogOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Mark Order as Returned</DialogTitle>
            </DialogHeader>
            <div className="space-y-4">
              <p className="text-sm text-muted-foreground">
                Order #{selectedOrder?.id.slice(-8).toUpperCase()}
              </p>
              <p className="text-sm">
                Are you sure you want to mark this order as returned? This indicates the customer has returned the items.
              </p>
              <div>
                <label htmlFor="returnReason" className="text-sm font-medium">
                  Return Reason (optional)
                </label>
                <textarea
                  id="returnReason"
                  className="w-full mt-1 px-3 py-2 border rounded-md text-sm"
                  placeholder="Enter return reason..."
                  rows={3}
                />
              </div>
              <div className="flex justify-end space-x-2">
                <button
                  onClick={() => setMarkAsReturnedDialogOpen(false)}
                  className="px-4 py-2 text-sm border rounded-md hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  onClick={() => {
                    const textarea = document.getElementById('returnReason') as HTMLTextAreaElement;
                    handleConfirmMarkAsReturned(textarea?.value);
                  }}
                  disabled={markReturnedMutation.isPending}
                  className="px-4 py-2 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50"
                >
                  {markReturnedMutation.isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin inline"/>
                      Marking...
                    </>
                  ) : (
                    'Mark as Returned'
                  )}
                </button>
              </div>
            </div>
          </DialogContent>
        </Dialog>
      )}
    </div>
  );
};

export default OrdersPage;
