import {Badge} from '@/components/ui/badge';
import {PaymentStatus} from '@/models/orders';

interface PaymentStatusBadgeProps {
  status: PaymentStatus;
}

export function PaymentStatusBadge({status}: PaymentStatusBadgeProps) {
  const getStatusConfig = (status: PaymentStatus) => {
    switch (status) {
      case PaymentStatus.Pending:
        return {
          label: 'Pending',
          variant: 'secondary' as const,
          className: 'bg-gray-100 text-gray-800 dark:bg-gray-800/20 dark:text-gray-500'
        };
      case PaymentStatus.Paid:
        return {
          label: 'Paid',
          variant: 'secondary' as const,
          className: 'bg-green-100 text-green-800 dark:bg-green-800/20 dark:text-green-500'
        };
      case PaymentStatus.Failed:
        return {
          label: 'Failed',
          variant: 'destructive' as const,
          className: 'bg-red-100 text-red-800 dark:bg-red-800/20 dark:text-red-500'
        };
      case PaymentStatus.Refunded:
        return {
          label: 'Refunded',
          variant: 'secondary' as const,
          className: 'bg-purple-100 text-purple-800 dark:bg-purple-800/20 dark:text-purple-500'
        };
      case PaymentStatus.PartiallyRefunded:
        return {
          label: 'Partially Refunded',
          variant: 'secondary' as const,
          className: 'bg-orange-100 text-orange-800 dark:bg-orange-800/20 dark:text-orange-500'
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
