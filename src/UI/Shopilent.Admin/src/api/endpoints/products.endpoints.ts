/**
 * Product API Endpoints
 */

import { buildUrlWithQuery, replacePath } from './types';

/**
 * Product endpoints
 */
export enum ProductEndpoint {
  // Basic CRUD
  GetPaged = '/v1/products/paged',
  GetById = '/v1/products/{id}',
  GetBySlug = '/v1/products/by-slug/{slug}',
  Create = '/v1/products',
  Update = '/v1/products/{id}',
  Delete = '/v1/products/{id}',

  // Actions
  UpdateStatus = '/v1/products/{id}/status',
  UpdateCategories = '/v1/products/{id}/categories',
  UpdateAttributes = '/v1/products/{id}/attributes',

  // Queries
  Search = '/v1/products/search',
  Datatable = '/v1/products/datatable',
}

/**
 * Get product endpoint by ID
 *
 * @param id - Product ID
 * @returns Product endpoint with ID
 */
export function getProductEndpoint(id: string): string {
  return replacePath(ProductEndpoint.GetById, { id });
}

/**
 * Get product endpoint by slug
 *
 * @param slug - Product slug
 * @returns Product endpoint with slug
 */
export function getProductBySlugEndpoint(slug: string): string {
  return replacePath(ProductEndpoint.GetBySlug, { slug });
}

/**
 * Get update product endpoint by ID
 *
 * @param id - Product ID
 * @returns Update product endpoint with ID
 */
export function updateProductEndpoint(id: string): string {
  return replacePath(ProductEndpoint.Update, { id });
}

/**
 * Get delete product endpoint by ID
 *
 * @param id - Product ID
 * @returns Delete product endpoint with ID
 */
export function deleteProductEndpoint(id: string): string {
  return replacePath(ProductEndpoint.Delete, { id });
}

/**
 * Get update product status endpoint by ID
 *
 * @param id - Product ID
 * @returns Update product status endpoint with ID
 */
export function updateProductStatusEndpoint(id: string): string {
  return replacePath(ProductEndpoint.UpdateStatus, { id });
}

/**
 * Get update product categories endpoint by ID
 *
 * @param id - Product ID
 * @returns Update product categories endpoint with ID
 */
export function updateProductCategoriesEndpoint(id: string): string {
  return replacePath(ProductEndpoint.UpdateCategories, { id });
}

/**
 * Get update product attributes endpoint by ID
 *
 * @param id - Product ID
 * @returns Update product attributes endpoint with ID
 */
export function updateProductAttributesEndpoint(id: string): string {
  return replacePath(ProductEndpoint.UpdateAttributes, { id });
}

/**
 * Build products paged URL with query parameters
 *
 * @param params - Pagination parameters
 * @returns Products paged URL with query string
 */
export function buildProductsPagedUrl(params: { page: number; pageSize: number }): string {
  return buildUrlWithQuery(ProductEndpoint.GetPaged, params);
}

/**
 * Build product search URL with query parameters
 *
 * @param params - Search parameters
 * @returns Product search URL with query string
 */
export function buildProductSearchUrl(params: { q: string }): string {
  return buildUrlWithQuery(ProductEndpoint.Search, params);
}
