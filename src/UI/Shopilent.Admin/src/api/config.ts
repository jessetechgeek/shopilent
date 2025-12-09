import axios from 'axios';
import { env } from '@/config/env';
import { AuthEndpoint } from '@/api/endpoints';

// Define base API response type
export interface ApiResponse<T> {
    succeeded: boolean;
    message: string;
    statusCode: number;
    data: T;
    errors: string[];
}

const apiClient = axios.create({
    baseURL: env.apiUrl,
    headers: {
        'Content-Type': 'application/json',
    },
});

// Add request interceptor for authentication
apiClient.interceptors.request.use(
    (config) => {
        const token = localStorage.getItem('accessToken');
        if (token) {
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => Promise.reject(error)
);

// Add response interceptor for token refresh
apiClient.interceptors.response.use(
    (response) => response,
    async (error) => {
        const originalRequest = error.config;

        // If error is 401 and we haven't already tried to refresh the token
        if (error.response?.status === 401 && !originalRequest._retry) {
            originalRequest._retry = true;

            try {
                // Logic to refresh token
                const refreshToken = localStorage.getItem('refreshToken');

                if (!refreshToken) {
                    // No refresh token available, redirect to login
                    window.location.href = '/login';
                    return Promise.reject(error);
                }

                const response = await axios.post<ApiResponse<{ accessToken: string; refreshToken: string }>>(
                    `${env.apiUrl}${AuthEndpoint.RefreshToken}`,
                    { refreshToken }
                );

                if (response.data.succeeded) {
                    // Update tokens in storage
                    localStorage.setItem('accessToken', response.data.data.accessToken);
                    localStorage.setItem('refreshToken', response.data.data.refreshToken);

                    // Update Authorization header for original request
                    originalRequest.headers.Authorization = `Bearer ${response.data.data.accessToken}`;

                    // Retry original request
                    return apiClient(originalRequest);
                } else {
                    // Token refresh failed, redirect to login
                    localStorage.removeItem('accessToken');
                    localStorage.removeItem('refreshToken');
                    window.location.href = '/login';
                    return Promise.reject(error);
                }
            } catch (refreshError) {
                // Error refreshing token, redirect to login
                localStorage.removeItem('accessToken');
                localStorage.removeItem('refreshToken');
                window.location.href = '/login';
                return Promise.reject(refreshError);
            }
        }

        return Promise.reject(error);
    }
);

export default apiClient;