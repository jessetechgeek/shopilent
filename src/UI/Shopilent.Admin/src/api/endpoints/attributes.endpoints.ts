/**
 * Attribute API Endpoints
 */

import { replacePath } from './types';

/**
 * Attribute endpoints
 */
export enum AttributeEndpoint {
  // Collections
  GetAll = '/v1/attributes',
  GetVariantAttributes = '/v1/attributes/variant',

  // CRUD
  GetById = '/v1/attributes/{id}',
  GetByName = '/v1/attributes/by-name/{name}',
  Create = '/v1/attributes',
  Update = '/v1/attributes/{id}',
  Delete = '/v1/attributes/{id}',

  // Queries
  Datatable = '/v1/attributes/datatable',
}

/**
 * Get attribute endpoint by ID
 *
 * @param id - Attribute ID
 * @returns Attribute endpoint with ID
 */
export function getAttributeEndpoint(id: string): string {
  return replacePath(AttributeEndpoint.GetById, { id });
}

/**
 * Get attribute endpoint by name
 *
 * @param name - Attribute name
 * @returns Attribute endpoint with name
 */
export function getAttributeByNameEndpoint(name: string): string {
  return replacePath(AttributeEndpoint.GetByName, { name });
}

/**
 * Get update attribute endpoint by ID
 *
 * @param id - Attribute ID
 * @returns Update attribute endpoint with ID
 */
export function updateAttributeEndpoint(id: string): string {
  return replacePath(AttributeEndpoint.Update, { id });
}

/**
 * Get delete attribute endpoint by ID
 *
 * @param id - Attribute ID
 * @returns Delete attribute endpoint with ID
 */
export function deleteAttributeEndpoint(id: string): string {
  return replacePath(AttributeEndpoint.Delete, { id });
}
