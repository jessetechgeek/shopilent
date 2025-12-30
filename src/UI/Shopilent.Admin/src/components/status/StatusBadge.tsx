import React from 'react';
import {CheckCircle2, AlertTriangle, XCircle} from 'lucide-react';
import {Badge} from '@/components/ui/badge';
import {cn} from '@/lib/utils';
import {HealthStatus} from '@/models/health';

interface StatusBadgeProps {
  status: HealthStatus;
  showIcon?: boolean;
  size?: 'sm' | 'md' | 'lg';
}

export const StatusBadge: React.FC<StatusBadgeProps> = ({
  status,
  showIcon = true,
  size = 'md',
}) => {
  const getStatusConfig = () => {
    switch (status) {
      case HealthStatus.Healthy:
        return {
          icon: CheckCircle2,
          className: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
          label: 'Healthy',
        };
      case HealthStatus.Degraded:
        return {
          icon: AlertTriangle,
          className: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200',
          label: 'Degraded',
        };
      case HealthStatus.Unhealthy:
        return {
          icon: XCircle,
          className: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200',
          label: 'Unhealthy',
        };
    }
  };

  const config = getStatusConfig();
  const Icon = config.icon;

  const sizeClasses = {
    sm: 'text-xs px-2 py-0.5',
    md: 'text-sm px-2.5 py-0.5',
    lg: 'text-base px-3 py-1',
  };

  const iconSizes = {
    sm: 'size-3',
    md: 'size-4',
    lg: 'size-5',
  };

  return (
    <Badge className={cn(config.className, sizeClasses[size])}>
      {showIcon && <Icon className={cn(iconSizes[size], 'mr-1')} />}
      {config.label}
    </Badge>
  );
};
