import React from 'react';
import {Card, CardContent, CardHeader, CardTitle} from '@/components/ui/card';
import {StatusBadge} from './StatusBadge';
import {HealthStatus} from '@/models/health';
import {Clock, AlertCircle} from 'lucide-react';
import {cn} from '@/lib/utils';

interface ServiceStatusCardProps {
  name: string;
  status: HealthStatus;
  duration?: string;
  description?: string;
  icon?: React.ReactNode;
  data?: Record<string, unknown>;
}

const formatDuration = (timeSpan: string): string => {
  // Parse .NET TimeSpan format "00:00:00.1234567" to milliseconds
  const parts = timeSpan.split(':');
  if (parts.length !== 3) return timeSpan;

  const hours = parseFloat(parts[0]);
  const minutes = parseFloat(parts[1]);
  const secondsAndMs = parseFloat(parts[2]);

  const totalMs = (hours * 3600 + minutes * 60 + secondsAndMs) * 1000;

  if (totalMs < 1) {
    return `${(totalMs * 1000).toFixed(0)}μs`;
  } else if (totalMs < 1000) {
    return `${totalMs.toFixed(0)}ms`;
  } else {
    return `${(totalMs / 1000).toFixed(2)}s`;
  }
};

export const ServiceStatusCard: React.FC<ServiceStatusCardProps> = ({
  name,
  status,
  duration,
  description,
  icon,
  data,
}) => {
  const isHealthy = status === HealthStatus.Healthy;
  const isDegraded = status === HealthStatus.Degraded;
  const isUnhealthy = status === HealthStatus.Unhealthy;

  return (
    <Card className={cn(
      "transition-all hover:shadow-md",
      isHealthy && "border-l-4 border-l-green-500",
      isDegraded && "border-l-4 border-l-yellow-500",
      isUnhealthy && "border-l-4 border-l-red-500"
    )}>
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between mb-2">
          <div className="flex items-center gap-2">
            {icon && <span className="text-muted-foreground">{icon}</span>}
            <CardTitle className="text-base font-semibold">{name}</CardTitle>
          </div>
          <StatusBadge status={status} size="sm" />
        </div>
      </CardHeader>

      <CardContent className="space-y-3">
        {duration && (
          <div className="flex items-center gap-2 text-sm">
            <Clock className="size-4 text-muted-foreground" />
            <span className="text-muted-foreground">Response:</span>
            <span className="font-mono font-medium">{formatDuration(duration)}</span>
          </div>
        )}

        {description && (
          <div className={cn(
            "flex items-start gap-2 text-sm p-2.5 rounded-md",
            isUnhealthy && "bg-red-50 dark:bg-red-950/20 border border-red-200 dark:border-red-900",
            isDegraded && "bg-yellow-50 dark:bg-yellow-950/20 border border-yellow-200 dark:border-yellow-900"
          )}>
            {(isUnhealthy || isDegraded) && <AlertCircle className="size-4 text-red-600 dark:text-red-400 mt-0.5 flex-shrink-0" />}
            <div>
              <p className={cn(
                "font-medium text-xs uppercase tracking-wide mb-0.5",
                isUnhealthy && "text-red-700 dark:text-red-400",
                isDegraded && "text-yellow-700 dark:text-yellow-400"
              )}>
                {isUnhealthy ? 'Error' : 'Message'}
              </p>
              <p className={cn(
                "text-sm",
                isUnhealthy && "text-red-900 dark:text-red-300",
                isDegraded && "text-yellow-900 dark:text-yellow-300"
              )}>
                {description}
              </p>
            </div>
          </div>
        )}

        {data && Object.keys(data).length > 0 && (
          <div className="text-sm">
            <p className="font-medium text-muted-foreground mb-2 text-xs uppercase tracking-wide">Additional Info</p>
            <pre className="rounded-md bg-muted p-3 text-xs overflow-x-auto border">
              {JSON.stringify(data, null, 2)}
            </pre>
          </div>
        )}
      </CardContent>
    </Card>
  );
};
