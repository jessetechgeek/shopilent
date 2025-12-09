/**
 * Administration API Endpoints
 */

/**
 * Administration endpoints (for admin tools)
 */
export enum AdministrationEndpoint {
  // Search management
  SearchRebuild = '/v1/administration/search/rebuild',

  // Cache management
  ClearCache = '/v1/administration/cache',
}
