/**
 * Centralized environment configuration
 * All environment variables should be accessed through this module
 */

export const env = {
  /** API base URL for backend requests */
  apiUrl: import.meta.env.VITE_API_URL || 'http://localhost:9801/api',

  /** S3/MinIO storage base URL for media files */
  s3Url: import.meta.env.VITE_S3_URL || 'http://localhost:9858',

  /** Development mode flag */
  isDev: import.meta.env.DEV,

  /** Production mode flag */
  isProd: import.meta.env.PROD,
} as const;

/**
 * Helper function to construct full S3 URL from image key
 * @param key - The image key/path in S3
 * @returns Full URL to the S3 object
 */
export const getS3Url = (key: string): string => {
  return `${env.s3Url}/shopilent/${key}`;
};
