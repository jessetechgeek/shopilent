import {Badge} from '@/components/ui/badge';
import {OrderStatus} from '@/models/orders';

interface OrderStatusBadgeProps {
  status: OrderStatus;
}

export function OrderStatusBadge({status}: OrderStatusBadgeProps) {
  const getStatusConfig = (status: OrderStatus) => {
    switch (status) {
      case OrderStatus.Pending:
        return {
          label: 'Pending',
          variant: 'secondary' as const,
          className: 'bg-gray-100 text-gray-800 dark:bg-gray-800/20 dark:text-gray-500'
        };
      case OrderStatus.Processing:
        return {
          label: 'Processing',
          variant: 'secondary' as const,
          className: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-800/20 dark:text-yellow-500'
        };
      case OrderStatus.Shipped:
        return {
          label: 'Shipped',
          variant: 'secondary' as const,
          className: 'bg-blue-100 text-blue-800 dark:bg-blue-800/20 dark:text-blue-500'
        };
      case OrderStatus.Delivered:
        return {
          label: 'Delivered',
          variant: 'secondary' as const,
          className: 'bg-green-100 text-green-800 dark:bg-green-800/20 dark:text-green-500'
        };
      case OrderStatus.Returned:
        return {
          label: 'Returned',
          variant: 'secondary' as const,
          className: 'bg-orange-100 text-orange-800 dark:bg-orange-800/20 dark:text-orange-500'
        };
      case OrderStatus.ReturnedAndRefunded:
        return {
          label: 'Returned & Refunded',
          variant: 'secondary' as const,
          className: 'bg-purple-100 text-purple-800 dark:bg-purple-800/20 dark:text-purple-500'
        };
      case OrderStatus.Cancelled:
        return {
          label: 'Cancelled',
          variant: 'destructive' as const,
          className: 'bg-red-100 text-red-800 dark:bg-red-800/20 dark:text-red-500'
        };
      default:
        return {
          label: 'Unknown',
          variant: 'secondary' as const,
          className: 'bg-gray-100 text-gray-800 dark:bg-gray-800/20 dark:text-gray-500'
        };
    }
  };

  const config = getStatusConfig(status);

  return (
    <Badge
      variant={config.variant}
      className={config.className}
    >
      {config.label}
    </Badge>
  );
}
