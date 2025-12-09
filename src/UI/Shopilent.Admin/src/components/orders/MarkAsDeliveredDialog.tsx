import {Loader2, CheckCircle, Package} from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import {Button} from '@/components/ui/button';
import {OrderDto} from '@/models/orders';
import {OrderStatusBadge} from './OrderStatusBadge';

interface MarkAsDeliveredDialogProps {
  order: OrderDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
  isLoading: boolean;
}

export function MarkAsDeliveredDialog({
                                        order,
                                        open,
                                        onOpenChange,
                                        onConfirm,
                                        isLoading
                                      }: MarkAsDeliveredDialogProps) {
  // Check if order is already delivered
  const isAlreadyDelivered = order?.status === 3; // OrderStatus.Delivered
  const isShipped = order?.status === 2; // OrderStatus.Shipped

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <div className="flex items-center space-x-2">
            <CheckCircle className="h-6 w-6 text-blue-600"/>
            <DialogTitle>Mark Order as Delivered</DialogTitle>
          </div>
          <DialogDescription>
            Confirm that order{' '}
            <span className="font-mono font-semibold">
                            #{order?.id.slice(-8).toUpperCase()}
                        </span>
            {' '}has been successfully delivered to the customer.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <label className="text-sm font-medium">Current Status</label>
            <div className="flex items-center space-x-2 p-3 bg-muted rounded-lg">
              {order && <OrderStatusBadge status={order.status}/>}
              {isAlreadyDelivered && (
                <div className="flex items-center space-x-1 text-blue-600">
                  <CheckCircle className="h-4 w-4"/>
                  <span className="text-sm">Already delivered</span>
                </div>
              )}
            </div>
          </div>

          {!isShipped && !isAlreadyDelivered && (
            <div className="rounded-lg bg-amber-50 dark:bg-amber-950 p-4">
              <h4 className="font-medium text-sm text-amber-900 dark:text-amber-100 mb-2">
                ⚠️ Notice
              </h4>
              <p className="text-sm text-amber-800 dark:text-amber-200">
                This order is not marked as shipped yet. Consider marking it as shipped first
                to maintain proper order flow tracking.
              </p>
            </div>
          )}

          {order && (
            <div className="rounded-lg bg-muted/50 p-4 space-y-2">
              <h4 className="font-medium text-sm flex items-center">
                <Package className="h-4 w-4 mr-2"/>
                Delivery Summary
              </h4>
              <div className="text-sm text-muted-foreground space-y-1">
                <div className="flex justify-between">
                  <span>Customer:</span>
                  <span>{order.userFullName}</span>
                </div>
                <div className="flex justify-between">
                  <span>Customer Email:</span>
                  <span>{order.userEmail}</span>
                </div>
                {order.trackingNumber && (
                  <div className="flex justify-between">
                    <span>Tracking Number:</span>
                    <span className="font-mono text-xs">{order.trackingNumber}</span>
                  </div>
                )}
                <div className="flex justify-between">
                  <span>Items:</span>
                  <span>{order.itemsCount} item{order.itemsCount !== 1 ? 's' : ''}</span>
                </div>
                <div className="flex justify-between">
                  <span>Total Value:</span>
                  <span className="font-medium">
                                        {new Intl.NumberFormat('en-US', {
                                          style: 'currency',
                                          currency: order.currency,
                                        }).format(order.total)}
                                    </span>
                </div>
              </div>
            </div>
          )}

          <div className="rounded-lg bg-blue-50 dark:bg-blue-950 p-4">
            <h4 className="font-medium text-sm text-blue-900 dark:text-blue-100 mb-2 flex items-center">
              <CheckCircle className="h-4 w-4 mr-2"/>
              What happens when marked as delivered?
            </h4>
            <ul className="text-sm text-blue-800 dark:text-blue-200 space-y-1">
              <li>• Order status will be changed to "Delivered"</li>
              <li>• Customer will receive a delivery confirmation email</li>
              <li>• Order will be marked as completed in the system</li>
              <li>• No further status changes will be allowed</li>
              {!isAlreadyDelivered && (
                <li>• This action cannot be undone</li>
              )}
            </ul>
          </div>

          {isAlreadyDelivered && (
            <div className="rounded-lg bg-green-50 dark:bg-green-950 p-4">
              <h4 className="font-medium text-sm text-green-900 dark:text-green-100 mb-2">
                ✅ Order Already Delivered
              </h4>
              <p className="text-sm text-green-800 dark:text-green-200">
                This order has already been marked as delivered. The customer has been
                notified and the order is completed.
              </p>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={isLoading}
          >
            Cancel
          </Button>
          <Button
            onClick={onConfirm}
            disabled={isLoading || isAlreadyDelivered}
            className="bg-blue-600 hover:bg-blue-700"
          >
            {isLoading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin"/>
                Marking as Delivered...
              </>
            ) : isAlreadyDelivered ? (
              <>
                <CheckCircle className="mr-2 h-4 w-4"/>
                Already Delivered
              </>
            ) : (
              <>
                <CheckCircle className="mr-2 h-4 w-4"/>
                Mark as Delivered
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
