export interface SearchRebuildRequest {
  initializeIndexes: boolean;
  indexProducts: boolean;
  forceReindex?: boolean;
}

export interface SearchRebuildResponse {
  isSuccess: boolean;
  message: string;
  indexesInitialized: boolean;
  productsIndexed: number;
}

export interface ClearCacheResponse {
  isSuccess: boolean;
  message: string;
}
