import {useState} from 'react';
import {Loader2, Truck, Package, CheckCircle} from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import {Button} from '@/components/ui/button';
import {Label} from '@/components/ui/label';
import {Input} from '@/components/ui/input';
import {OrderDto} from '@/models/orders';
import {OrderStatusBadge} from './OrderStatusBadge';

interface MarkAsShippedDialogProps {
  order: OrderDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (trackingNumber?: string) => void;
  isLoading: boolean;
}

export function MarkAsShippedDialog({
                                      order,
                                      open,
                                      onOpenChange,
                                      onConfirm,
                                      isLoading
                                    }: MarkAsShippedDialogProps) {
  const [trackingNumber, setTrackingNumber] = useState<string>('');

  const handleConfirm = () => {
    onConfirm(trackingNumber.trim() || undefined);
  };

  const handleOpenChange = (newOpen: boolean) => {
    if (!newOpen) {
      setTrackingNumber('');
    }
    onOpenChange(newOpen);
  };

  // Check if order is already shipped
  const isAlreadyShipped = order?.status === 2; // OrderStatus.Shipped

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <div className="flex items-center space-x-2">
            <Truck className="h-6 w-6 text-green-600"/>
            <DialogTitle>Mark Order as Shipped</DialogTitle>
          </div>
          <DialogDescription>
            Mark order{' '}
            <span className="font-mono font-semibold">
                            #{order?.id.slice(-8).toUpperCase()}
                        </span>
            {' '}as shipped and optionally add tracking information.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="current-status">Current Status</Label>
            <div className="flex items-center space-x-2 p-3 bg-muted rounded-lg">
              {order && <OrderStatusBadge status={order.status}/>}
              {isAlreadyShipped && (
                <div className="flex items-center space-x-1 text-green-600">
                  <CheckCircle className="h-4 w-4"/>
                  <span className="text-sm">Already shipped</span>
                </div>
              )}
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="tracking-number">
              Tracking Number <span className="text-muted-foreground">(Optional)</span>
            </Label>
            <Input
              id="tracking-number"
              placeholder="Enter tracking number (e.g., 1Z999AA1234567890)"
              value={trackingNumber}
              onChange={(e) => setTrackingNumber(e.target.value)}
              className="font-mono"
            />
            <p className="text-xs text-muted-foreground">
              Common formats: UPS (1Z...), FedEx (1234 5678 9012), DHL (1234567890), USPS (9400...)
            </p>
          </div>

          {order?.trackingNumber && (
            <div className="space-y-2">
              <Label>Current Tracking Number</Label>
              <div className="p-3 bg-muted rounded-lg text-sm font-mono">
                {order.trackingNumber}
              </div>
            </div>
          )}

          {order && (
            <div className="rounded-lg bg-muted/50 p-4 space-y-2">
              <h4 className="font-medium text-sm flex items-center">
                <Package className="h-4 w-4 mr-2"/>
                Order Summary
              </h4>
              <div className="text-sm text-muted-foreground space-y-1">
                <div className="flex justify-between">
                  <span>Customer:</span>
                  <span>{order.userFullName}</span>
                </div>
                <div className="flex justify-between">
                  <span>Shipping Method:</span>
                  <span>{order.shippingMethod || 'Standard'}</span>
                </div>
                <div className="flex justify-between">
                  <span>Items:</span>
                  <span>{order.itemsCount} item{order.itemsCount !== 1 ? 's' : ''}</span>
                </div>
                <div className="flex justify-between">
                  <span>Total Amount:</span>
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

          <div className="rounded-lg bg-green-50 dark:bg-green-950 p-4">
            <h4 className="font-medium text-sm text-green-900 dark:text-green-100 mb-2 flex items-center">
              <CheckCircle className="h-4 w-4 mr-2"/>
              What happens next?
            </h4>
            <ul className="text-sm text-green-800 dark:text-green-200 space-y-1">
              <li>• Order status will be changed to "Shipped"</li>
              <li>• Customer will receive a shipping notification email</li>
              {trackingNumber.trim() && (
                <li>• Tracking number will be included in the notification</li>
              )}
              <li>• Customer can track their order progress</li>
            </ul>
          </div>

          {isAlreadyShipped && (
            <div className="rounded-lg bg-amber-50 dark:bg-amber-950 p-4">
              <h4 className="font-medium text-sm text-amber-900 dark:text-amber-100 mb-2">
                ℹ️ Note
              </h4>
              <p className="text-sm text-amber-800 dark:text-amber-200">
                This order is already marked as shipped. Confirming will update the tracking number
                {trackingNumber.trim() ? ' and send a new notification to the customer' : ''}.
              </p>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => handleOpenChange(false)}
            disabled={isLoading}
          >
            Cancel
          </Button>
          <Button
            onClick={handleConfirm}
            disabled={isLoading}
            className="bg-green-600 hover:bg-green-700"
          >
            {isLoading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin"/>
                {isAlreadyShipped ? 'Updating...' : 'Marking as Shipped...'}
              </>
            ) : (
              <>
                <Truck className="mr-2 h-4 w-4"/>
                {isAlreadyShipped ? 'Update Tracking' : 'Mark as Shipped'}
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
