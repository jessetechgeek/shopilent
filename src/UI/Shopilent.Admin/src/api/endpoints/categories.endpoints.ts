/**
 * Category API Endpoints
 */

import { replacePath } from './types';

/**
 * Category endpoints
 */
export enum CategoryEndpoint {
  // Collections
  GetAll = '/v1/categories/all',
  GetRoot = '/v1/categories/root',

  // CRUD
  GetById = '/v1/categories/{id}',
  GetBySlug = '/v1/categories/by-slug/{slug}',
  Create = '/v1/categories',
  Update = '/v1/categories/{id}',
  Delete = '/v1/categories/{id}',

  // Hierarchical
  GetChildren = '/v1/categories/{id}/children',
  UpdateParent = '/v1/categories/{id}/parent',

  // Actions
  UpdateStatus = '/v1/categories/{id}/status',

  // Queries
  Datatable = '/v1/categories/datatable',
}

/**
 * Get category endpoint by ID
 *
 * @param id - Category ID
 * @returns Category endpoint with ID
 */
export function getCategoryEndpoint(id: string): string {
  return replacePath(CategoryEndpoint.GetById, { id });
}

/**
 * Get category endpoint by slug
 *
 * @param slug - Category slug
 * @returns Category endpoint with slug
 */
export function getCategoryBySlugEndpoint(slug: string): string {
  return replacePath(CategoryEndpoint.GetBySlug, { slug });
}

/**
 * Get child categories endpoint by parent ID
 *
 * @param parentId - Parent category ID
 * @returns Child categories endpoint with parent ID
 */
export function getChildCategoriesEndpoint(parentId: string): string {
  return replacePath(CategoryEndpoint.GetChildren, { id: parentId });
}

/**
 * Get update category endpoint by ID
 *
 * @param id - Category ID
 * @returns Update category endpoint with ID
 */
export function updateCategoryEndpoint(id: string): string {
  return replacePath(CategoryEndpoint.Update, { id });
}

/**
 * Get delete category endpoint by ID
 *
 * @param id - Category ID
 * @returns Delete category endpoint with ID
 */
export function deleteCategoryEndpoint(id: string): string {
  return replacePath(CategoryEndpoint.Delete, { id });
}

/**
 * Get update category status endpoint by ID
 *
 * @param id - Category ID
 * @returns Update category status endpoint with ID
 */
export function updateCategoryStatusEndpoint(id: string): string {
  return replacePath(CategoryEndpoint.UpdateStatus, { id });
}

/**
 * Get update category parent endpoint by ID
 *
 * @param id - Category ID
 * @returns Update category parent endpoint with ID
 */
export function updateCategoryParentEndpoint(id: string): string {
  return replacePath(CategoryEndpoint.UpdateParent, { id });
}
