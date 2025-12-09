import apiClient, { ApiResponse } from './config';
import { SearchRebuildRequest, SearchRebuildResponse, ClearCacheResponse } from '@/models/tools';
import { AdministrationEndpoint } from '@/api/endpoints';

export const administrationApi = {
  searchRebuild: async (request: SearchRebuildRequest): Promise<ApiResponse<SearchRebuildResponse>> => {
    const response = await apiClient.post<ApiResponse<SearchRebuildResponse>>(
      AdministrationEndpoint.SearchRebuild,
      request
    );
    return response.data;
  },

  clearCache: async (): Promise<ApiResponse<ClearCacheResponse>> => {
    const response = await apiClient.delete<ApiResponse<ClearCacheResponse>>(
      AdministrationEndpoint.ClearCache
    );
    return response.data;
  },
};