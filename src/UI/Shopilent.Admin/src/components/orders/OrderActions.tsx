import {Button} from '@/components/ui/button';
import {Tooltip, TooltipContent, TooltipProvider, TooltipTrigger} from '@/components/ui/tooltip';
import {
  Eye,
  Edit,
  Truck,
  RefreshCw,
  XCircle,
  RotateCcw,
  CreditCard,
  Send,
  CheckCircle,
  PackageX
} from 'lucide-react';
import {useNavigate} from 'react-router-dom';
import {OrderDto, OrderStatus, PaymentStatus} from '@/models/orders';

interface OrderActionsProps {
  order: OrderDto;
  onViewDetails: (order: OrderDto) => void;
  onUpdateStatus: (order: OrderDto) => void;
  onUpdatePayment: (order: OrderDto) => void;
  onUpdateTracking: (order: OrderDto) => void;
  onMarkAsShipped: (order: OrderDto) => void;
  onMarkAsDelivered: (order: OrderDto) => void;
  onMarkAsReturned: (order: OrderDto) => void;
  onRefund: (order: OrderDto) => void;
  onPartialRefund: (order: OrderDto) => void;
  onCancel: (order: OrderDto) => void;
}

export function OrderActions({
                               order,
                               onViewDetails,
                               onUpdateStatus,
                               onUpdatePayment,
                               onUpdateTracking,
                               onMarkAsShipped,
                               onMarkAsDelivered,
                               onMarkAsReturned,
                               onRefund,
                               onPartialRefund,
                               onCancel,
                             }: OrderActionsProps) {
  const navigate = useNavigate();

  // Determine what actions are available based on order status
  const canUpdateTracking = order.status === OrderStatus.Shipped;
  const canMarkAsShipped = order.status === OrderStatus.Pending || order.status === OrderStatus.Processing;
  const canMarkAsDelivered = order.status === OrderStatus.Shipped;
  const canMarkAsReturned = order.status === OrderStatus.Delivered;
  const canUpdateStatus = order.status !== OrderStatus.Cancelled && order.status !== OrderStatus.ReturnedAndRefunded;
  const canUpdatePayment = order.paymentStatus !== PaymentStatus.Refunded && order.status !== OrderStatus.Cancelled;
  const canCancel = order.status !== OrderStatus.Cancelled &&
    order.status !== OrderStatus.ReturnedAndRefunded &&
    order.status !== OrderStatus.Delivered &&
    order.status !== OrderStatus.Returned;
  const canRefund = order.paymentStatus === PaymentStatus.Paid &&
    order.status !== OrderStatus.Cancelled;
  const canPartialRefund = (order.paymentStatus === PaymentStatus.Paid || order.paymentStatus === PaymentStatus.PartiallyRefunded) &&
    order.status !== OrderStatus.Cancelled;

  // Handle edit navigation
  const handleEdit = () => {
    navigate(`/orders/edit/${order.id}`);
  };

  return (
    <TooltipProvider>
      <div className="flex items-center justify-end space-x-1">
        {/* View Details */}
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => onViewDetails(order)}
            >
              <Eye className="size-4"/>
              <span className="sr-only">View details</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>View Details</p>
          </TooltipContent>
        </Tooltip>

        {/* Edit Button */}
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              onClick={handleEdit}
            >
              <Edit className="size-4"/>
              <span className="sr-only">Edit order</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>Edit Order</p>
          </TooltipContent>
        </Tooltip>

        {/* Update Status */}
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              disabled={!canUpdateStatus}
              onClick={() => canUpdateStatus && onUpdateStatus(order)}
            >
              <RefreshCw className="size-4"/>
              <span className="sr-only">Update status</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>{canUpdateStatus ? 'Update Status' : 'Cannot update status (cancelled/returned & refunded)'}</p>
          </TooltipContent>
        </Tooltip>

        {/* Update Payment Status */}
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              disabled={!canUpdatePayment}
              onClick={() => canUpdatePayment && onUpdatePayment(order)}
            >
              <CreditCard className="size-4"/>
              <span className="sr-only">Update payment</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>{canUpdatePayment ? 'Update Payment Status' : 'Cannot update payment (refunded/cancelled)'}</p>
          </TooltipContent>
        </Tooltip>

        {/* Mark as Shipped */}
        {canMarkAsShipped && (
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                onClick={() => onMarkAsShipped(order)}
                className="text-green-600 hover:text-green-700 hover:bg-green-50"
              >
                <Send className="size-4"/>
                <span className="sr-only">Mark as shipped</span>
              </Button>
            </TooltipTrigger>
            <TooltipContent>
              <p>Mark as Shipped</p>
            </TooltipContent>
          </Tooltip>
        )}

        {/* Update Tracking */}
        {canUpdateTracking && (
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                onClick={() => onUpdateTracking(order)}
              >
                <Truck className="size-4"/>
                <span className="sr-only">Update tracking</span>
              </Button>
            </TooltipTrigger>
            <TooltipContent>
              <p>Update Tracking Number</p>
            </TooltipContent>
          </Tooltip>
        )}

        {/* Mark as Delivered */}
        {canMarkAsDelivered && (
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                onClick={() => onMarkAsDelivered(order)}
                className="text-blue-600 hover:text-blue-700 hover:bg-blue-50"
              >
                <CheckCircle className="size-4"/>
                <span className="sr-only">Mark as delivered</span>
              </Button>
            </TooltipTrigger>
            <TooltipContent>
              <p>Mark as Delivered</p>
            </TooltipContent>
          </Tooltip>
        )}

        {/* Mark as Returned */}
        {canMarkAsReturned && (
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="ghost"
                size="icon"
                onClick={() => onMarkAsReturned(order)}
                className="text-purple-600 hover:text-purple-700 hover:bg-purple-50"
              >
                <PackageX className="size-4"/>
                <span className="sr-only">Mark as returned</span>
              </Button>
            </TooltipTrigger>
            <TooltipContent>
              <p>Mark as Returned</p>
            </TooltipContent>
          </Tooltip>
        )}

        {/* Full Refund Order */}
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              disabled={!canRefund}
              onClick={() => canRefund && onRefund(order)}
              className={canRefund ? "text-orange-600 hover:text-orange-700 hover:bg-orange-50" : ""}
            >
              <RotateCcw className="size-4"/>
              <span className="sr-only">Full refund order</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>{canRefund ? 'Process Full Refund' : 'Cannot refund (not paid/already cancelled/refunded)'}</p>
          </TooltipContent>
        </Tooltip>

        {/* Partial Refund Order */}
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              disabled={!canPartialRefund}
              onClick={() => canPartialRefund && onPartialRefund(order)}
              className={canPartialRefund ? "text-yellow-600 hover:text-yellow-700 hover:bg-yellow-50" : ""}
            >
              <CreditCard className="size-4"/>
              <span className="sr-only">Partial refund order</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>{canPartialRefund ? 'Process Partial Refund' : 'Cannot partial refund'}</p>
          </TooltipContent>
        </Tooltip>

        {/* Cancel Order */}
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              disabled={!canCancel}
              onClick={() => canCancel && onCancel(order)}
              className={canCancel ? "text-red-600 hover:text-red-700 hover:bg-red-50" : ""}
            >
              <XCircle className="size-4"/>
              <span className="sr-only">Cancel order</span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>{canCancel ? 'Cancel Order' : 'Cannot cancel (already cancelled/returned & refunded/delivered)'}</p>
          </TooltipContent>
        </Tooltip>
      </div>
    </TooltipProvider>
  );
}
