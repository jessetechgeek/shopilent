export enum HealthStatus {
  Healthy = 'Healthy',
  Degraded = 'Degraded',
  Unhealthy = 'Unhealthy',
}

export interface HealthCheckEntry {
  status: HealthStatus;
  description?: string;
  duration: string;
  data?: Record<string, unknown>;
}

export interface HealthCheckResponse {
  status: HealthStatus;
  totalDuration: string;
  entries: Record<string, HealthCheckEntry>;
}

export interface ServiceStatus {
  name: string;
  status: HealthStatus;
  duration?: string;
  description?: string;
  data?: Record<string, unknown>;
}
