import {useState} from 'react';
import {Loader2, Package} from 'lucide-react';
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
import {OrderDto, OrderStatus} from '@/models/orders';
import {OrderStatusBadge} from './OrderStatusBadge';

interface UpdateOrderStatusDialogProps {
  order: OrderDto | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onConfirm: (status: OrderStatus, reason?: string) => void;
  isLoading: boolean;
}

const STATUS_OPTIONS = [
  {value: OrderStatus.Pending.toString(), label: 'Pending', description: 'Order is waiting to be processed'},
  {value: OrderStatus.Processing.toString(), label: 'Processing', description: 'Order is being prepared'},
  {value: OrderStatus.Shipped.toString(), label: 'Shipped', description: 'Order has been shipped to customer'},
  {value: OrderStatus.Delivered.toString(), label: 'Delivered', description: 'Order has been delivered to customer'},
  {value: OrderStatus.Cancelled.toString(), label: 'Cancelled', description: 'Order has been cancelled'},
  {value: OrderStatus.Refunded.toString(), label: 'Refunded', description: 'Order has been refunded'},
];

export function UpdateOrderStatusDialog({
                                          order,
                                          open,
                                          onOpenChange,
                                          onConfirm,
                                          isLoading
                                        }: UpdateOrderStatusDialogProps) {
  const [selectedStatus, setSelectedStatus] = useState<string>('');
  const [reason, setReason] = useState<string>('');

  const handleConfirm = () => {
    if (selectedStatus) {
      const status = parseInt(selectedStatus) as OrderStatus;
      onConfirm(status, reason.trim() || undefined);
    }
  };

  const handleOpenChange = (newOpen: boolean) => {
    if (!newOpen) {
      setSelectedStatus('');
      setReason('');
    }
    onOpenChange(newOpen);
  };

  const selectedStatusOption = STATUS_OPTIONS.find(opt => opt.value === selectedStatus);
  const currentStatusOption = STATUS_OPTIONS.find(opt => opt.value === order?.status.toString());
  const isFormValid = selectedStatus && selectedStatus !== order?.status.toString();

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <div className="flex items-center space-x-2">
            <Package className="h-6 w-6 text-blue-600"/>
            <DialogTitle>Update Order Status</DialogTitle>
          </div>
          <DialogDescription>
            Update the status for order{' '}
            <span className="font-mono font-semibold">
                            #{order?.id.slice(-8).toUpperCase()}
                        </span>
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="current-status">Current Status</Label>
            <div className="flex items-center space-x-2 p-3 bg-muted rounded-lg">
              {order && <OrderStatusBadge status={order.status}/>}
              <span className="text-sm text-muted-foreground">
                                {currentStatusOption?.description}
                            </span>
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="new-status">New Status</Label>
            <Select
              value={selectedStatus}
              onValueChange={setSelectedStatus}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select new status"/>
              </SelectTrigger>
              <SelectContent>
                {STATUS_OPTIONS.map((option) => (
                  <SelectItem
                    key={option.value}
                    value={option.value}
                    disabled={option.value === order?.status.toString()}
                  >
                    <div className="flex flex-col">
                      <span className="font-medium">{option.label}</span>
                      <span className="text-xs text-muted-foreground">
                                                {option.description}
                                            </span>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label htmlFor="reason">Reason (Optional)</Label>
            <Textarea
              id="reason"
              placeholder="Enter reason for status change..."
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={3}
            />
          </div>

          {selectedStatusOption && (
            <div className="rounded-lg bg-blue-50 dark:bg-blue-950 p-4 space-y-2">
              <h4 className="font-medium text-sm text-blue-900 dark:text-blue-100">
                Status Change Preview
              </h4>
              <div className="text-sm text-blue-800 dark:text-blue-200">
                Order will be changed to: <strong>{selectedStatusOption.label}</strong>
              </div>
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
                <Package className="mr-2 h-4 w-4"/>
                Update Status
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
