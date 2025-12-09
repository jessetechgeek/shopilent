import apiClient, {ApiResponse} from './config';
import {
    ProductVariantDto,
    CreateProductVariantRequest,
    UpdateProductVariantRequest,
    UpdateVariantStockRequest,
    UpdateVariantStatusRequest,
    UpdateVariantAttributesRequest,
    VariantDatatableRequest
} from '@/models/catalog';
import {DataTableResult} from '@/models/common';
import {
    VariantEndpoint,
    getProductVariantsEndpoint,
    createVariantEndpoint,
    getVariantEndpoint,
    getVariantBySkuEndpoint,
    updateVariantEndpoint,
    deleteVariantEndpoint,
    updateVariantStockEndpoint,
    updateVariantStatusEndpoint,
    updateVariantAttributesEndpoint
} from '@/api/endpoints';

export const variantApi = {
    // Get variants for a product
    getProductVariants: (productId: string) =>
        apiClient.get<ApiResponse<ProductVariantDto[]>>(getProductVariantsEndpoint(productId)),

    // Get variant by ID
    getVariantById: (id: string) =>
        apiClient.get<ApiResponse<ProductVariantDto>>(getVariantEndpoint(id)),

    // Get variant by SKU
    getVariantBySku: (sku: string) =>
        apiClient.get<ApiResponse<ProductVariantDto>>(getVariantBySkuEndpoint(sku)),

    // Get variants in stock
    getVariantsInStock: () =>
        apiClient.get<ApiResponse<ProductVariantDto[]>>(VariantEndpoint.GetInStock),

    // Get variants for datatable
    getVariantsForDatatable: (request: VariantDatatableRequest) =>
        apiClient.post<ApiResponse<DataTableResult<ProductVariantDto>>>(VariantEndpoint.Datatable, request),

    // Create a new variant with image support
    createVariant: (variant: CreateProductVariantRequest) => {
        // Always use FormData for requests with potential files
        const formData = new FormData();

        // Add each variant field individually to the FormData
        Object.entries(variant).forEach(([key, value]) => {
            if (key === 'file') {
                // Handle file array separately
                if (Array.isArray(value)) {
                    value.forEach(file => {
                        formData.append('file', file);
                    });
                }
            } else if (key === 'images') {
                // Handle images metadata
                if (Array.isArray(value)) {
                    value.forEach((img, index) => {
                        if (img.altText) formData.append(`images[${index}].altText`, img.altText);
                        if (img.isDefault !== undefined) formData.append(`images[${index}].isDefault`, String(img.isDefault));
                        if (img.displayOrder !== undefined) formData.append(`images[${index}].displayOrder`, String(img.displayOrder));
                    });
                }
            } else if (value !== undefined) {
                // Handle arrays and objects by converting to JSON strings
                if (Array.isArray(value) || (typeof value === 'object' && value !== null)) {
                    formData.append(key, JSON.stringify(value));
                } else {
                    formData.append(key, String(value));
                }
            }
        });

        // Send multipart/form-data request
        return apiClient.post<ApiResponse<ProductVariantDto>>(createVariantEndpoint(variant.productId), formData, {
            headers: {
                'Content-Type': 'multipart/form-data',
            },
        });
    },

    // Update a variant with image support
    updateVariant: (id: string, variant: UpdateProductVariantRequest) => {
        // Always use FormData for requests with potential files
        const formData = new FormData();

        // Add each variant field individually to the FormData
        Object.entries(variant).forEach(([key, value]) => {
            if (key === 'file') {
                // Handle file array separately
                if (Array.isArray(value)) {
                    value.forEach(file => {
                        formData.append('file', file);
                    });
                }
            } else if (key === 'images') {
                // Handle images metadata
                if (Array.isArray(value)) {
                    value.forEach((img, index) => {
                        if (img.altText) formData.append(`images[${index}].altText`, img.altText);
                        if (img.isDefault !== undefined) formData.append(`images[${index}].isDefault`, String(img.isDefault));
                        if (img.displayOrder !== undefined) formData.append(`images[${index}].displayOrder`, String(img.displayOrder));
                        if (img.imageKey) formData.append(`images[${index}].imageKey`, img.imageKey);
                    });
                }
            } else if (key === 'imagesToRemove') {
                // Handle images to remove
                if (Array.isArray(value)) {
                    value.forEach(imageKey => {
                        formData.append('imagesToRemove', imageKey);
                    });
                }
            } else if (value !== undefined) {
                // Handle arrays and objects by converting to JSON strings
                if (Array.isArray(value) || (typeof value === 'object' && value !== null)) {
                    formData.append(key, JSON.stringify(value)); // ✅ This will handle imageOrders same as updateProduct
                } else {
                    formData.append(key, String(value));
                }
            }
        });

        // Send multipart/form-data request
        return apiClient.put<ApiResponse<ProductVariantDto>>(updateVariantEndpoint(id), formData, {
            headers: {
                'Content-Type': 'multipart/form-data',
            },
        });
    },

    // Delete a variant
    deleteVariant: (id: string) =>
        apiClient.delete<ApiResponse<boolean>>(deleteVariantEndpoint(id)),

    // Update variant stock - FIXED: Use 'quantity' property instead of 'stockQuantity'
    updateVariantStock: (id: string, stock: UpdateVariantStockRequest) =>
        apiClient.put<ApiResponse<ProductVariantDto>>(updateVariantStockEndpoint(id), stock),

    // Update variant status
    updateVariantStatus: (id: string, status: UpdateVariantStatusRequest) =>
        apiClient.put<ApiResponse<ProductVariantDto>>(updateVariantStatusEndpoint(id), status),

    // Update variant attributes
    updateVariantAttributes: (id: string, attributes: UpdateVariantAttributesRequest) =>
        apiClient.put<ApiResponse<ProductVariantDto>>(updateVariantAttributesEndpoint(id), attributes),
};