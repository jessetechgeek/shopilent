import axios from 'axios';
import {env} from '@/config/env';
import {HealthEndpoint} from './endpoints/health.endpoints';
import type {HealthCheckResponse} from '@/models/health';

// Create a separate axios instance for health endpoint (not under /api prefix)
const healthClient = axios.create({
  baseURL: env.apiUrl.replace('/api', ''), // Remove /api prefix
  headers: {
    'Content-Type': 'application/json',
  },
  withCredentials: true,
});

export const healthApi = {
  getHealth: async (): Promise<HealthCheckResponse> => {
    try {
      const response = await healthClient.get<HealthCheckResponse>(HealthEndpoint.GetHealth);
      return response.data;
    } catch (error) {
      // Health endpoint returns 503 when unhealthy, but we still want the data
      if (axios.isAxiosError(error) && error.response?.status === 503) {
        return error.response.data as HealthCheckResponse;
      }
      throw error;
    }
  },
};
