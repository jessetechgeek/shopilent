/**
 * Shared types and utilities for API endpoints
 */

/**
 * Type for query parameters
 */
export type QueryParams = Record<string, string | number | boolean | undefined | (string | number)[]>;

/**
 * Generic helper to build URLs with query parameters
 * Handles single values and arrays, with proper URL encoding
 *
 * @param endpoint - The base endpoint URL
 * @param params - Query parameters object
 * @returns URL with query string appended
 *
 * @example
 * buildUrlWithQuery('/v1/products/paged', { page: 1, pageSize: 20 })
 * // Returns: '/v1/products/paged?page=1&pageSize=20'
 */
export function buildUrlWithQuery(endpoint: string, params?: QueryParams): string {
  if (!params) return endpoint;

  const queryParts: string[] = [];

  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined) return;

    if (Array.isArray(value)) {
      value.forEach(v => {
        queryParts.push(`${key}=${encodeURIComponent(String(v))}`);
      });
    } else {
      queryParts.push(`${key}=${encodeURIComponent(String(value))}`);
    }
  });

  const queryString = queryParts.join('&');
  return queryString ? `${endpoint}?${queryString}` : endpoint;
}

/**
 * Generic helper to replace path parameters in endpoint URLs
 * Automatically encodes parameter values
 *
 * @param endpoint - The endpoint URL with {param} placeholders
 * @param params - Object with parameter names and values
 * @returns URL with parameters replaced
 *
 * @example
 * replacePath('/v1/products/{id}', { id: '123' })
 * // Returns: '/v1/products/123'
 */
export function replacePath(endpoint: string, params: Record<string, string>): string {
  let url = endpoint;
  Object.entries(params).forEach(([key, value]) => {
    url = url.replace(`{${key}}`, encodeURIComponent(value));
  });
  return url;
}
