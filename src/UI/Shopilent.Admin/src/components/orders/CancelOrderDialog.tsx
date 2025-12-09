import {useState} from 'react';
import {Loader2, XCircle} from 'lucide-react';
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
import {Textarea} from '@/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {OrderDto} from '@/models/orders';

interface CancelOrderDialogProps {
  order: OrderDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (reason?: string) => void;
  isLoading: boolean;
}

const CANCEL_REASONS = [
  {value: 'customer_request', label: 'Customer Request'},
  {value: 'payment_failed', label: 'Payment Failed'},
  {value: 'out_of_stock', label: 'Out of Stock'},
  {value: 'shipping_issues', label: 'Shipping Issues'},
  {value: 'fraudulent_order', label: 'Fraudulent Order'},
  {value: 'duplicate_order', label: 'Duplicate Order'},
  {value: 'business_decision', label: 'Business Decision'},
  {value: 'other', label: 'Other'},
];

export function CancelOrderDialog({
                                    order,
                                    open,
                                    onOpenChange,
                                    onConfirm,
                                    isLoading
                                  }: CancelOrderDialogProps) {
  const [selectedReason, setSelectedReason] = useState<string>('');
  const [customReason, setCustomReason] = useState<string>('');

  const handleConfirm = () => {
    const reason = selectedReason === 'other' ? customReason :
      CANCEL_REASONS.find(r => r.value === selectedReason)?.label || '';
    onConfirm(reason);
  };

  const handleOpenChange = (newOpen: boolean) => {
    if (!newOpen) {
      setSelectedReason('');
      setCustomReason('');
    }
    onOpenChange(newOpen);
  };

  const isFormValid = selectedReason && (selectedReason !== 'other' || customReason.trim());

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <div className="flex items-center space-x-2">
            <XCircle className="h-6 w-6 text-red-600"/>
            <DialogTitle>Cancel Order</DialogTitle>
          </div>
          <DialogDescription>
            You are about to cancel order{' '}
            <span className="font-mono font-semibold">
                            #{order?.id.slice(-8).toUpperCase()}
                        </span>
            . This action cannot be undone.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="cancel-reason">Cancellation Reason</Label>
            <Select
              value={selectedReason}
              onValueChange={setSelectedReason}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select a reason for cancellation"/>
              </SelectTrigger>
              <SelectContent>
                {CANCEL_REASONS.map((reason) => (
                  <SelectItem key={reason.value} value={reason.value}>
                    {reason.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {selectedReason === 'other' && (
            <div className="space-y-2">
              <Label htmlFor="custom-reason">Custom Reason</Label>
              <Textarea
                id="custom-reason"
                placeholder="Please specify the reason for cancellation..."
                value={customReason}
                onChange={(e) => setCustomReason(e.target.value)}
                rows={3}
              />
            </div>
          )}

          {order && (
            <div className="rounded-lg bg-muted/50 p-4 space-y-2">
              <h4 className="font-medium text-sm">Order Summary</h4>
              <div className="text-sm text-muted-foreground space-y-1">
                <div className="flex justify-between">
                  <span>Customer:</span>
                  <span>{order.userFullName}</span>
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
                <div className="flex justify-between">
                  <span>Items:</span>
                  <span>{order.itemsCount} item{order.itemsCount !== 1 ? 's' : ''}</span>
                </div>
              </div>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => handleOpenChange(false)}
            disabled={isLoading}
          >
            Keep Order
          </Button>
          <Button
            variant="destructive"
            onClick={handleConfirm}
            disabled={!isFormValid || isLoading}
          >
            {isLoading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin"/>
                Cancelling...
              </>
            ) : (
              <>
                <XCircle className="mr-2 h-4 w-4"/>
                Cancel Order
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
