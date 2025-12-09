/**
 * Variant API Endpoints
 */

import { replacePath } from './types';

/**
 * Variant endpoints
 */
export enum VariantEndpoint {
  // Product variants (hierarchical)
  GetProductVariants = '/v1/products/{productId}/variants',
  CreateVariant = '/v1/products/{productId}/variants',

  // Variant CRUD
  GetById = '/v1/variants/{id}',
  GetBySku = '/v1/variants/by-sku/{sku}',
  Update = '/v1/variants/{id}',
  Delete = '/v1/variants/{id}',

  // Queries
  GetInStock = '/v1/variants/in-stock',
  Datatable = '/v1/variants/datatable',

  // Actions
  UpdateStock = '/v1/variants/{id}/stock',
  UpdateStatus = '/v1/variants/{id}/status',
  UpdateAttributes = '/v1/variants/{id}/attributes',
}

/**
 * Get product variants endpoint by product ID
 *
 * @param productId - Product ID
 * @returns Product variants endpoint with product ID
 */
export function getProductVariantsEndpoint(productId: string): string {
  return replacePath(VariantEndpoint.GetProductVariants, { productId });
}

/**
 * Get create variant endpoint by product ID
 *
 * @param productId - Product ID
 * @returns Create variant endpoint with product ID
 */
export function createVariantEndpoint(productId: string): string {
  return replacePath(VariantEndpoint.CreateVariant, { productId });
}

/**
 * Get variant endpoint by ID
 *
 * @param id - Variant ID
 * @returns Variant endpoint with ID
 */
export function getVariantEndpoint(id: string): string {
  return replacePath(VariantEndpoint.GetById, { id });
}

/**
 * Get variant endpoint by SKU
 *
 * @param sku - Variant SKU
 * @returns Variant endpoint with SKU
 */
export function getVariantBySkuEndpoint(sku: string): string {
  return replacePath(VariantEndpoint.GetBySku, { sku });
}

/**
 * Get update variant endpoint by ID
 *
 * @param id - Variant ID
 * @returns Update variant endpoint with ID
 */
export function updateVariantEndpoint(id: string): string {
  return replacePath(VariantEndpoint.Update, { id });
}

/**
 * Get delete variant endpoint by ID
 *
 * @param id - Variant ID
 * @returns Delete variant endpoint with ID
 */
export function deleteVariantEndpoint(id: string): string {
  return replacePath(VariantEndpoint.Delete, { id });
}

/**
 * Get update variant stock endpoint by ID
 *
 * @param id - Variant ID
 * @returns Update variant stock endpoint with ID
 */
export function updateVariantStockEndpoint(id: string): string {
  return replacePath(VariantEndpoint.UpdateStock, { id });
}

/**
 * Get update variant status endpoint by ID
 *
 * @param id - Variant ID
 * @returns Update variant status endpoint with ID
 */
export function updateVariantStatusEndpoint(id: string): string {
  return replacePath(VariantEndpoint.UpdateStatus, { id });
}

/**
 * Get update variant attributes endpoint by ID
 *
 * @param id - Variant ID
 * @returns Update variant attributes endpoint with ID
 */
export function updateVariantAttributesEndpoint(id: string): string {
  return replacePath(VariantEndpoint.UpdateAttributes, { id });
}
