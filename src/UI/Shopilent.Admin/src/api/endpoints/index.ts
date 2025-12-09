/**
 * API Endpoints - Centralized endpoint definitions
 *
 * This file exports all API endpoint enums and helper functions
 * for type-safe endpoint management across the application.
 */

// Export utilities
export * from './types';

// Export auth endpoints
export { AuthEndpoint, AuthUserEndpoint } from './auth.endpoints';
export * from './auth.endpoints';

// Export product endpoints
export { ProductEndpoint } from './products.endpoints';
export * from './products.endpoints';

// Export variant endpoints
export { VariantEndpoint } from './variants.endpoints';
export * from './variants.endpoints';

// Export category endpoints
export { CategoryEndpoint } from './categories.endpoints';
export * from './categories.endpoints';

// Export attribute endpoints
export { AttributeEndpoint } from './attributes.endpoints';
export * from './attributes.endpoints';

// Export order endpoints
export { OrderEndpoint } from './orders.endpoints';
export * from './orders.endpoints';

// Export user endpoints
export { UserEndpoint } from './users.endpoints';
export * from './users.endpoints';

// Export administration endpoints
export { AdministrationEndpoint } from './administration.endpoints';
export * from './administration.endpoints';
