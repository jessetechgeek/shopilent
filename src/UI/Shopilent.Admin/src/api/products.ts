import apiClient, {ApiResponse} from './config';
import {
    ProductDto,
    ProductDetailDto,
    ProductDataTableDto,
    CreateProductRequest,
    UpdateProductRequest,
    UpdateProductStatusRequest,
    ProductDatatableRequest,
    UpdateProductCategoriesRequest,
    UpdateProductAttributesRequest
} from '@/models/catalog';
import {DataTableResult, PaginatedResult} from '@/models/common';
import {
    ProductEndpoint,
    buildProductsPagedUrl,
    getProductEndpoint,
    getProductBySlugEndpoint,
    buildProductSearchUrl,
    updateProductEndpoint,
    deleteProductEndpoint,
    updateProductStatusEndpoint,
    updateProductCategoriesEndpoint,
    updateProductAttributesEndpoint
} from '@/api/endpoints';

export const productApi = {
    // Get all products (paginated)
    getProducts: (page: number = 1, pageSize: number = 10) =>
        apiClient.get<ApiResponse<PaginatedResult<ProductDto>>>(buildProductsPagedUrl({ page, pageSize })),

    // Get product by ID
    getProductById: (id: string) =>
        apiClient.get<ApiResponse<ProductDetailDto>>(getProductEndpoint(id)),

    // Get product by slug
    getProductBySlug: (slug: string) =>
        apiClient.get<ApiResponse<ProductDetailDto>>(getProductBySlugEndpoint(slug)),

    // Search products
    searchProducts: (query: string) =>
        apiClient.get<ApiResponse<ProductDto[]>>(buildProductSearchUrl({ q: query })),

    // Get products for datatable (with filtering, sorting, pagination)
    getProductsForDatatable: (request: ProductDatatableRequest) =>
        apiClient.post<ApiResponse<DataTableResult<ProductDataTableDto>>>(ProductEndpoint.Datatable, request),

    // Create a new product
    // createProduct: (product: CreateProductRequest) =>
    // apiClient.post<ApiResponse<ProductDto>>('/v1/products', product),

    createProduct: (product: CreateProductRequest, files?: File[]) => {
        // Always use FormData for the request
        const formData = new FormData();

        // Add each product field individually to the FormData
        Object.entries(product).forEach(([key, value]) => {
            if (value !== undefined) {
                // Handle arrays and objects by converting to JSON strings
                if (Array.isArray(value) || (typeof value === 'object' && value !== null)) {
                    formData.append(key, JSON.stringify(value));
                } else {
                    formData.append(key, String(value));
                }
            }
        });

        // Add each file to the FormData
        if (files && files.length > 0) {
            files.forEach(file => {
                // Use "Images" to match IFormFile or IFormFileCollection parameter in your controller
                formData.append('file', file);
            });
        }

        // Send multipart/form-data request
        return apiClient.post<ApiResponse<ProductDto>>(ProductEndpoint.Create, formData, {
            headers: {
                'Content-Type': 'multipart/form-data',
            },
        });
    },

    updateProduct: (id: string, product: UpdateProductRequest, files?: File[]) => {
        // Always use FormData for the request
        const formData = new FormData();

        // Add each product field individually to the FormData
        Object.entries(product).forEach(([key, value]) => {
            console.log('updateProduct', key, value);
            if (value !== undefined) {
                // Handle arrays and objects by converting to JSON strings
                if (Array.isArray(value) || (typeof value === 'object' && value !== null)) {

                    formData.append(key, JSON.stringify(value)); // ✅ This will handle imageOrders
                } else {
                    formData.append(key, String(value));
                }
            }
        });

        // Add each file to the FormData
        if (files && files.length > 0) {
            files.forEach(file => {
                // Use "file" to match IFormFile parameter in your controller
                formData.append('file', file);
            });
        }

        // Send multipart/form-data request
        return apiClient.put<ApiResponse<ProductDto>>(updateProductEndpoint(id), formData, {
            headers: {
                'Content-Type': 'multipart/form-data',
            },
        });
    },

    // Delete a product
    deleteProduct: (id: string) =>
        apiClient.delete<ApiResponse<boolean>>(deleteProductEndpoint(id)),

    // Activate/deactivate a product
    updateProductStatus: (id: string, status: UpdateProductStatusRequest) =>
        apiClient.put<ApiResponse<ProductDto>>(updateProductStatusEndpoint(id), status),

    // Update product categories
    updateProductCategories: (id: string, categories: UpdateProductCategoriesRequest) =>
        apiClient.put<ApiResponse<ProductDto>>(updateProductCategoriesEndpoint(id), categories),

    // Update product attributes
    updateProductAttributes: (id: string, attributes: UpdateProductAttributesRequest) =>
        apiClient.put<ApiResponse<ProductDto>>(updateProductAttributesEndpoint(id), attributes),
};

