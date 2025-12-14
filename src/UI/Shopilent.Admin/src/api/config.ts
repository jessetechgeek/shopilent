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
        'X-Client-Platform': 'web',
    },
    withCredentials: true,
});

// Add request interceptor for authentication
apiClient.interceptors.request.use(
    (config) => {
        // Tokens are now sent via HttpOnly cookies automatically
        // No need to manually add Authorization header for web clients
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
                // Refresh token is sent via HttpOnly cookie automatically
                const response = await axios.post<ApiResponse<{ accessToken: string; refreshToken: string }>>(
                    `${env.apiUrl}${AuthEndpoint.RefreshToken}`,
                    {}, // Empty body - refresh token is in cookie
                    {
                        headers: {
                            'X-Client-Platform': 'web',
                        },
                        withCredentials: true,
                    }
                );

                if (response.data.succeeded) {
                    // Tokens are now in HttpOnly cookies, set by the server
                    // No need to store them in localStorage

                    // Retry original request
                    return apiClient(originalRequest);
                } else {
                    // Token refresh failed, redirect to login
                    localStorage.removeItem('user');
                    window.location.href = '/login';
                    return Promise.reject(error);
                }
            } catch (refreshError) {
                // Error refreshing token, redirect to login
                localStorage.removeItem('user');
                window.location.href = '/login';
                return Promise.reject(refreshError);
            }
        }

        return Promise.reject(error);
    }
);

export default apiClient;