/**
 * Authentication API Endpoints
 */

import { buildUrlWithQuery } from './types';

/**
 * Authentication endpoints
 */
export enum AuthEndpoint {
  Login = '/v1/auth/login',
  Logout = '/v1/auth/logout',
  RefreshToken = '/v1/auth/refresh-token',
  ForgotPassword = '/v1/auth/forgot-password',
  VerifyResetToken = '/v1/auth/verify-reset-token',
  ResetPassword = '/v1/auth/reset-password',
}

/**
 * Auth-related user endpoints (password, profile)
 */
export enum AuthUserEndpoint {
  ChangePassword = '/v1/users/change-password',
}

/**
 * Build verify reset token URL with token query parameter
 *
 * @param token - The reset token
 * @returns URL with token query parameter
 */
export function buildVerifyResetTokenUrl(token: string): string {
  return buildUrlWithQuery(AuthEndpoint.VerifyResetToken, { token });
}
