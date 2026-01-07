/**
 * Centralized environment configuration
 * All environment variables should be accessed through this module
 *
 * In production (Docker), runtime configuration is loaded from window.ENV
 * In development, uses Vite's import.meta.env
 */

// Extend Window interface for TypeScript
declare global {
  interface Window {
    ENV?: {
      VITE_API_URL: string;
    };
  }
}

// Helper function to get environment variable with fallback
const getEnvVar = (key: 'VITE_API_URL', fallback: string): string => {
  // In production (Docker), use runtime config from window.ENV
  if (window.ENV && window.ENV[key]) {
    return window.ENV[key];
  }

  // In development, use Vite's import.meta.env
  return import.meta.env[key] || fallback;
};

export const env = {
  /** API base URL for backend requests */
  apiUrl: getEnvVar('VITE_API_URL', 'http://localhost:9801/api'),

  /** Development mode flag */
  isDev: import.meta.env.DEV,

  /** Production mode flag */
  isProd: import.meta.env.PROD,
} as const;
