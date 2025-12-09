import {useState} from 'react';
import {Loader2, Truck, Package} from 'lucide-react';
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

interface UpdateTrackingDialogProps {
  order: OrderDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (trackingNumber: string) => void;
  isLoading: boolean;
}

export function UpdateTrackingDialog({
                                       order,
                                       open,
                                       onOpenChange,
                                       onConfirm,
                                       isLoading
                                     }: UpdateTrackingDialogProps) {
  const [trackingNumber, setTrackingNumber] = useState<string>('');

  const handleConfirm = () => {
    if (trackingNumber.trim()) {
      onConfirm(trackingNumber.trim());
    }
  };

  const handleOpenChange = (newOpen: boolean) => {
    if (!newOpen) {
      setTrackingNumber('');
    } else if (order?.trackingNumber) {
      setTrackingNumber(order.trackingNumber);
    }
    onOpenChange(newOpen);
  };

  const isFormValid = trackingNumber.trim() && trackingNumber.trim() !== (order?.trackingNumber || '');

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <div className="flex items-center space-x-2">
            <Truck className="h-6 w-6 text-green-600"/>
            <DialogTitle>Update Tracking Information</DialogTitle>
          </div>
          <DialogDescription>
            Update tracking information for order{' '}
            <span className="font-mono font-semibold">
                            #{order?.id.slice(-8).toUpperCase()}
                        </span>
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="current-tracking">Current Tracking Number</Label>
            <div className="p-3 bg-muted rounded-lg text-sm">
              {order?.trackingNumber || 'No tracking number set'}
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="tracking-number">New Tracking Number</Label>
            <Input
              id="tracking-number"
              placeholder="Enter tracking number (e.g., 1Z999AA1234567890)"
              value={trackingNumber}
              onChange={(e) => setTrackingNumber(e.target.value)}
              className="font-mono"
            />
            <p className="text-xs text-muted-foreground">
              Common formats: UPS (1Z...), FedEx (1234 5678 9012), DHL (1234567890)
            </p>
          </div>

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
              </div>
            </div>
          )}

          <div className="rounded-lg bg-blue-50 dark:bg-blue-950 p-4">
            <h4 className="font-medium text-sm text-blue-900 dark:text-blue-100 mb-2">
              📋 Note
            </h4>
            <p className="text-sm text-blue-800 dark:text-blue-200">
              Adding a tracking number will automatically mark the order as shipped and notify the customer.
            </p>
          </div>
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
            disabled={!isFormValid || isLoading}
          >
            {isLoading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin"/>
                Updating...
              </>
            ) : (
              <>
                <Truck className="mr-2 h-4 w-4"/>
                Update Tracking
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
