import React, {useState, useEffect} from 'react';
import {Card, CardContent, CardHeader, CardTitle} from '@/components/ui/card';
import {StatusBadge} from './StatusBadge';
import {HealthStatus} from '@/models/health';
import {RefreshCw, Activity} from 'lucide-react';
import {cn} from '@/lib/utils';

interface SystemOverviewProps {
  overallStatus: HealthStatus;
  healthyCount: number;
  degradedCount: number;
  unhealthyCount: number;
  totalDuration?: string;
  lastUpdated: number; // timestamp in milliseconds
  isAutoRefresh: boolean;
  isRefreshing: boolean;
}

const formatRelativeTime = (date: Date): string => {
  const now = new Date();
  const diffInSeconds = Math.floor((now.getTime() - date.getTime()) / 1000);

  if (diffInSeconds < 5) return 'just now';
  if (diffInSeconds < 60) return `${diffInSeconds} seconds ago`;
  if (diffInSeconds < 120) return '1 minute ago';
  if (diffInSeconds < 3600) return `${Math.floor(diffInSeconds / 60)} minutes ago`;
  if (diffInSeconds < 7200) return '1 hour ago';
  return `${Math.floor(diffInSeconds / 3600)} hours ago`;
};

const formatDuration = (timeSpan: string): string => {
  // Parse .NET TimeSpan format "00:00:00.1234567" to milliseconds
  const parts = timeSpan.split(':');
  if (parts.length !== 3) return timeSpan;

  const hours = parseFloat(parts[0]);
  const minutes = parseFloat(parts[1]);
  const secondsAndMs = parseFloat(parts[2]);

  const totalMs = (hours * 3600 + minutes * 60 + secondsAndMs) * 1000;

  if (totalMs < 1000) {
    return `${totalMs.toFixed(0)}ms`;
  } else {
    return `${(totalMs / 1000).toFixed(2)}s`;
  }
};

export const SystemOverview: React.FC<SystemOverviewProps> = ({
  overallStatus,
  healthyCount,
  degradedCount,
  unhealthyCount,
  totalDuration,
  lastUpdated,
  isAutoRefresh,
  isRefreshing,
}) => {
  const lastUpdatedDate = new Date(lastUpdated);
  const [relativeTime, setRelativeTime] = useState(formatRelativeTime(lastUpdatedDate));

  useEffect(() => {
    // Update relative time every second
    const interval = setInterval(() => {
      setRelativeTime(formatRelativeTime(new Date(lastUpdated)));
    }, 1000);

    return () => clearInterval(interval);
  }, [lastUpdated]);

  const totalServices = healthyCount + degradedCount + unhealthyCount;

  return (
    <Card className="border-2">
      <CardHeader className="pb-4">
        <CardTitle className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <Activity className="size-6 text-muted-foreground" />
            <div>
              <h3 className="text-lg font-semibold">System Health</h3>
              <p className="text-sm text-muted-foreground">Overall infrastructure status</p>
            </div>
          </div>
          <StatusBadge status={overallStatus} size="lg" />
        </CardTitle>
      </CardHeader>

      <CardContent className="space-y-6">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div className="p-4 rounded-lg border bg-card">
            <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide mb-1">Total Services</p>
            <p className="text-3xl font-bold">{totalServices}</p>
          </div>

          <div className="p-4 rounded-lg border bg-gradient-to-br from-green-50 to-green-100/50 dark:from-green-950/20 dark:to-green-900/10">
            <p className="text-xs font-medium text-green-700 dark:text-green-400 uppercase tracking-wide mb-1">Healthy</p>
            <p className="text-3xl font-bold text-green-700 dark:text-green-400">{healthyCount}</p>
          </div>

          <div className="p-4 rounded-lg border bg-gradient-to-br from-yellow-50 to-yellow-100/50 dark:from-yellow-950/20 dark:to-yellow-900/10">
            <p className="text-xs font-medium text-yellow-700 dark:text-yellow-400 uppercase tracking-wide mb-1">Degraded</p>
            <p className="text-3xl font-bold text-yellow-700 dark:text-yellow-400">{degradedCount}</p>
          </div>

          <div className="p-4 rounded-lg border bg-gradient-to-br from-red-50 to-red-100/50 dark:from-red-950/20 dark:to-red-900/10">
            <p className="text-xs font-medium text-red-700 dark:text-red-400 uppercase tracking-wide mb-1">Unhealthy</p>
            <p className="text-3xl font-bold text-red-700 dark:text-red-400">{unhealthyCount}</p>
          </div>
        </div>

        <div className="flex items-center justify-between text-sm pt-4 border-t">
          <div className="flex items-center gap-2 text-muted-foreground">
            <RefreshCw className={cn('size-4', isRefreshing && 'animate-spin text-blue-600 dark:text-blue-400')} />
            <span>{isRefreshing ? 'Updating...' : `Updated ${relativeTime}`}</span>
          </div>
          {totalDuration && (
            <div className="flex items-center gap-1.5 text-muted-foreground">
              <span className="text-xs">Check time:</span>
              <span className="font-medium">{formatDuration(totalDuration)}</span>
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
};
