import apiClient, {ApiResponse} from './config';
import {
    CategoryDto,
    CreateCategoryRequest,
    UpdateCategoryRequest,
    UpdateCategoryStatusRequest,
    UpdateCategoryParentRequest,
    CategoryDatatableRequest, CategoryDataTableDto
} from '@/models/catalog';
import {DataTableResult} from '@/models/common';
import {
    CategoryEndpoint,
    getCategoryEndpoint,
    getCategoryBySlugEndpoint,
    getChildCategoriesEndpoint,
    updateCategoryEndpoint,
    deleteCategoryEndpoint,
    updateCategoryStatusEndpoint,
    updateCategoryParentEndpoint
} from '@/api/endpoints';

export const categoryApi = {
    // Get all categories (paginated)
    getCategories: () =>
        apiClient.get<ApiResponse<CategoryDto[]>>(CategoryEndpoint.GetAll),

    // Get all root categories
    getRootCategories: () =>
        apiClient.get<ApiResponse<CategoryDto[]>>(CategoryEndpoint.GetRoot),

    // Get category by ID
    getCategoryById: (id: string) =>
        apiClient.get<ApiResponse<CategoryDto>>(getCategoryEndpoint(id)),

    // Get category by slug
    getCategoryBySlug: (slug: string) =>
        apiClient.get<ApiResponse<CategoryDto>>(getCategoryBySlugEndpoint(slug)),

    // Get child categories
    getChildCategories: (parentId: string) =>
        apiClient.get<ApiResponse<CategoryDto[]>>(getChildCategoriesEndpoint(parentId)),

    // Get categories for datatable (with filtering, sorting, pagination)
    getCategoriesForDatatable: (request: CategoryDatatableRequest) =>
        apiClient.post<ApiResponse<DataTableResult<CategoryDataTableDto>>>(CategoryEndpoint.Datatable, request),

    // Create a new category
    createCategory: (category: CreateCategoryRequest) =>
        apiClient.post<ApiResponse<CategoryDto>>(CategoryEndpoint.Create, category),

    // Update a category
    updateCategory: (id: string, category: UpdateCategoryRequest) =>
        apiClient.put<ApiResponse<CategoryDto>>(updateCategoryEndpoint(id), category),

    // Delete a category
    deleteCategory: (id: string) =>
        apiClient.delete<ApiResponse<boolean>>(deleteCategoryEndpoint(id)),

    // Activate/deactivate a category
    updateCategoryStatus: (id: string, status: UpdateCategoryStatusRequest) =>
        apiClient.put<ApiResponse<CategoryDto>>(updateCategoryStatusEndpoint(id), status),

    // Change parent category
    updateCategoryParent: (id: string, parent: UpdateCategoryParentRequest) =>
        apiClient.put<ApiResponse<CategoryDto>>(updateCategoryParentEndpoint(id), parent),
};