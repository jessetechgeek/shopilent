import apiClient, { ApiResponse } from './config';
import { LoginRequest, LoginResponse } from '../models/auth';
import { AuthEndpoint, AuthUserEndpoint, buildVerifyResetTokenUrl } from '@/api/endpoints';

export const authApi = {
    // Existing functions
    login: (data: LoginRequest) =>
        apiClient.post<ApiResponse<LoginResponse>>(AuthEndpoint.Login, data),

    logout: () =>
        apiClient.post<ApiResponse<string>>(AuthEndpoint.Logout, {}),

    refreshToken: () =>
        apiClient.post<ApiResponse<LoginResponse>>(AuthEndpoint.RefreshToken, {}),

    changePassword: (currentPassword: string, newPassword: string, confirmPassword: string) =>
        apiClient.put<ApiResponse<string>>(AuthUserEndpoint.ChangePassword, {
            currentPassword,
            newPassword,
            confirmPassword,
        }),

    // New functions for password reset
    forgotPassword: (email: string) =>
        apiClient.post<ApiResponse<string>>(AuthEndpoint.ForgotPassword, { email }),

    verifyResetToken: (token: string) =>
        apiClient.get<ApiResponse<boolean>>(buildVerifyResetTokenUrl(token)),

    resetPassword: (token: string, newPassword: string, confirmPassword: string) =>
        apiClient.post<ApiResponse<string>>(AuthEndpoint.ResetPassword, {
            token,
            newPassword,
            confirmPassword,
        }),
};