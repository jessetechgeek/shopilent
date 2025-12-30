import React, {useState, useEffect} from 'react';
import {useQuery} from '@tanstack/react-query';
import {Activity, RefreshCw, Database, Server, Search} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {Switch} from '@/components/ui/switch';
import {Label} from '@/components/ui/label';
import {Alert, AlertDescription} from '@/components/ui/alert';
import {Card} from '@/components/ui/card';
import {SystemOverview} from '@/components/status/SystemOverview';
import {ServiceStatusCard} from '@/components/status/ServiceStatusCard';
import {healthApi} from '@/api/health';
import {HealthStatus} from '@/models/health';
import {useTitle} from '@/hooks/useTitle';

const StatusPage: React.FC = () => {
  useTitle('System Status');
  const [isAutoRefreshEnabled, setIsAutoRefreshEnabled] = useState(true);
  const [isPageVisible, setIsPageVisible] = useState(!document.hidden);

  // Handle page visibility to pause auto-refresh when tab is not visible
  useEffect(() => {
    const handleVisibilityChange = () => {
      setIsPageVisible(!document.hidden);
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
  }, []);

  const {data, isLoading, isError, error, refetch, dataUpdatedAt, isFetching} = useQuery({
    queryKey: ['health'],
    queryFn: async () => {
      return await healthApi.getHealth();
    },
    refetchInterval: isAutoRefreshEnabled && isPageVisible ? 30000 : false,
    staleTime: 15000,
    retry: 2,
  });

  const handleManualRefresh = () => {
    refetch();
  };

  const handleAutoRefreshToggle = (checked: boolean) => {
    setIsAutoRefreshEnabled(checked);
  };

  // Process health check data
  const processHealthData = () => {
    if (!data?.entries) {
      return {
        databaseServices: [],
        cacheServices: [],
        searchServices: [],
        healthyCount: 0,
        degradedCount: 0,
        unhealthyCount: 0,
      };
    }

    const entries = Object.entries(data.entries);

    const databaseServices = entries
      .filter(([key]) => key.startsWith('postgresql-'))
      .map(([key, value]) => ({
        name: key,
        status: value.status,
        duration: value.duration,
        description: value.description,
        data: value.data,
      }));

    const cacheServices = entries
      .filter(([key]) => key === 'redis')
      .map(([key, value]) => ({
        name: key,
        status: value.status,
        duration: value.duration,
        description: value.description,
        data: value.data,
      }));

    const searchServices = entries
      .filter(([key]) => key === 'meilisearch')
      .map(([key, value]) => ({
        name: key,
        status: value.status,
        duration: value.duration,
        description: value.description,
        data: value.data,
      }));

    // Calculate statistics
    const healthyCount = entries.filter(([, value]) => value.status === HealthStatus.Healthy).length;
    const degradedCount = entries.filter(([, value]) => value.status === HealthStatus.Degraded).length;
    const unhealthyCount = entries.filter(([, value]) => value.status === HealthStatus.Unhealthy).length;

    return {
      databaseServices,
      cacheServices,
      searchServices,
      healthyCount,
      degradedCount,
      unhealthyCount,
    };
  };

  const {
    databaseServices,
    cacheServices,
    searchServices,
    healthyCount,
    degradedCount,
    unhealthyCount,
  } = processHealthData();

  // Loading state
  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-3">
          <Activity className="size-8 text-muted-foreground" />
          <div>
            <h1 className="text-3xl font-bold tracking-tight">System Status</h1>
            <p className="text-muted-foreground">
              Real-time health monitoring of infrastructure services
            </p>
          </div>
        </div>

        <Card className="p-8">
          <div className="flex items-center justify-center">
            <RefreshCw className="size-8 animate-spin text-muted-foreground" />
            <span className="ml-3 text-muted-foreground">Loading system status...</span>
          </div>
        </Card>
      </div>
    );
  }

  // Error state
  if (isError) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-3">
          <Activity className="size-8 text-muted-foreground" />
          <div>
            <h1 className="text-3xl font-bold tracking-tight">System Status</h1>
            <p className="text-muted-foreground">
              Real-time health monitoring of infrastructure services
            </p>
          </div>
        </div>

        <Alert variant="destructive">
          <AlertDescription className="flex items-center justify-between">
            <span>
              Failed to fetch health status: {error instanceof Error ? error.message : 'Unknown error'}
            </span>
            <Button onClick={handleManualRefresh} variant="outline" size="sm">
              <RefreshCw className="size-4 mr-2" />
              Retry
            </Button>
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  // Empty state
  if (!data || Object.keys(data.entries).length === 0) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-3">
          <Activity className="size-8 text-muted-foreground" />
          <div>
            <h1 className="text-3xl font-bold tracking-tight">System Status</h1>
            <p className="text-muted-foreground">
              Real-time health monitoring of infrastructure services
            </p>
          </div>
        </div>

        <Alert>
          <AlertDescription>
            No health check services configured.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div className="flex items-center gap-3">
          <Activity className="size-8 text-muted-foreground" />
          <div>
            <h1 className="text-3xl font-bold tracking-tight">System Status</h1>
            <p className="text-muted-foreground">
              Real-time health monitoring of infrastructure services
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <Button
            onClick={handleManualRefresh}
            disabled={isFetching}
            variant="outline"
            size="sm"
            className="gap-2"
          >
            <RefreshCw className={`size-4 ${isFetching ? 'animate-spin' : ''}`} />
            {isFetching ? 'Refreshing...' : 'Refresh'}
          </Button>

          <div className="flex items-center gap-2 px-3 py-2 rounded-md border bg-card">
            <Switch
              id="auto-refresh"
              checked={isAutoRefreshEnabled}
              onCheckedChange={handleAutoRefreshToggle}
            />
            <Label htmlFor="auto-refresh" className="text-sm cursor-pointer whitespace-nowrap">
              Auto-refresh (30s)
            </Label>
          </div>
        </div>
      </div>

      {/* System Overview */}
      <SystemOverview
        overallStatus={data.status}
        healthyCount={healthyCount}
        degradedCount={degradedCount}
        unhealthyCount={unhealthyCount}
        totalDuration={data.totalDuration}
        lastUpdated={dataUpdatedAt}
        isAutoRefresh={isAutoRefreshEnabled && isPageVisible}
        isRefreshing={isFetching}
      />

      {/* Database Services */}
      {databaseServices.length > 0 && (
        <div className="space-y-4">
          <div className="flex items-center gap-2 pb-2 border-b">
            <Database className="size-5 text-muted-foreground" />
            <h2 className="text-xl font-semibold">Database Services</h2>
            <span className="text-sm text-muted-foreground ml-auto">
              {databaseServices.length} {databaseServices.length === 1 ? 'instance' : 'instances'}
            </span>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {databaseServices.map((service) => (
              <ServiceStatusCard
                key={service.name}
                name={service.name}
                status={service.status}
                duration={service.duration}
                description={service.description}
                icon={<Database className="size-4" />}
                data={service.data}
              />
            ))}
          </div>
        </div>
      )}

      {/* Cache Services */}
      {cacheServices.length > 0 && (
        <div className="space-y-4">
          <div className="flex items-center gap-2 pb-2 border-b">
            <Server className="size-5 text-muted-foreground" />
            <h2 className="text-xl font-semibold">Cache Services</h2>
            <span className="text-sm text-muted-foreground ml-auto">
              {cacheServices.length} {cacheServices.length === 1 ? 'instance' : 'instances'}
            </span>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {cacheServices.map((service) => (
              <ServiceStatusCard
                key={service.name}
                name={service.name}
                status={service.status}
                duration={service.duration}
                description={service.description}
                icon={<Server className="size-4" />}
                data={service.data}
              />
            ))}
          </div>
        </div>
      )}

      {/* Search Services */}
      {searchServices.length > 0 && (
        <div className="space-y-4">
          <div className="flex items-center gap-2 pb-2 border-b">
            <Search className="size-5 text-muted-foreground" />
            <h2 className="text-xl font-semibold">Search Services</h2>
            <span className="text-sm text-muted-foreground ml-auto">
              {searchServices.length} {searchServices.length === 1 ? 'instance' : 'instances'}
            </span>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {searchServices.map((service) => (
              <ServiceStatusCard
                key={service.name}
                name={service.name}
                status={service.status}
                duration={service.duration}
                description={service.description}
                icon={<Search className="size-4" />}
                data={service.data}
              />
            ))}
          </div>
        </div>
      )}
    </div>
  );
};

export default StatusPage;
