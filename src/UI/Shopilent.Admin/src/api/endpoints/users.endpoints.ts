/**
 * User/Customer API Endpoints
 */

import { replacePath } from './types';

/**
 * User endpoints (for customer management)
 */
export enum UserEndpoint {
  // CRUD
  GetById = '/v1/users/{id}',
  Update = '/v1/users/{id}',

  // Actions
  UpdateStatus = '/v1/users/{id}/status',
  UpdateRole = '/v1/users/{id}/role',

  // Queries
  Datatable = '/v1/users/datatable',
}

/**
 * Get user endpoint by ID
 *
 * @param id - User ID
 * @returns User endpoint with ID
 */
export function getUserEndpoint(id: string): string {
  return replacePath(UserEndpoint.GetById, { id });
}

/**
 * Get update user endpoint by ID
 *
 * @param id - User ID
 * @returns Update user endpoint with ID
 */
export function updateUserEndpoint(id: string): string {
  return replacePath(UserEndpoint.Update, { id });
}

/**
 * Get update user status endpoint by ID
 *
 * @param id - User ID
 * @returns Update user status endpoint with ID
 */
export function updateUserStatusEndpoint(id: string): string {
  return replacePath(UserEndpoint.UpdateStatus, { id });
}

/**
 * Get update user role endpoint by ID
 *
 * @param id - User ID
 * @returns Update user role endpoint with ID
 */
export function updateUserRoleEndpoint(id: string): string {
  return replacePath(UserEndpoint.UpdateRole, { id });
}
