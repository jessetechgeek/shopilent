import apiClient, { ApiResponse } from './config';
import {
    AttributeDto,
    CreateAttributeRequest,
    UpdateAttributeRequest,
    AttributeDatatableRequest,
    AttributeDataTableDto
} from '@/models/catalog';
import { DataTableResult } from '@/models/common';
import {
    AttributeEndpoint,
    getAttributeEndpoint,
    getAttributeByNameEndpoint,
    updateAttributeEndpoint,
    deleteAttributeEndpoint
} from '@/api/endpoints';

export const attributeApi = {
    // Get all attributes
    getAttributes: () =>
        apiClient.get<ApiResponse<AttributeDto[]>>(AttributeEndpoint.GetAll),

    // Get attribute by ID
    getAttributeById: (id: string) =>
        apiClient.get<ApiResponse<AttributeDto>>(getAttributeEndpoint(id)),

    // Get attribute by name
    getAttributeByName: (name: string) =>
        apiClient.get<ApiResponse<AttributeDto>>(getAttributeByNameEndpoint(name)),

    // Get all variant attributes
    getVariantAttributes: () =>
        apiClient.get<ApiResponse<AttributeDto[]>>(AttributeEndpoint.GetVariantAttributes),

    // Get attributes for datatable (with filtering, sorting, pagination)
    getAttributesForDatatable: (request: AttributeDatatableRequest) =>
        apiClient.post<ApiResponse<DataTableResult<AttributeDataTableDto>>>(AttributeEndpoint.Datatable, request),

    // Create a new attribute
    createAttribute: (attribute: CreateAttributeRequest) =>
        apiClient.post<ApiResponse<AttributeDto>>(AttributeEndpoint.Create, attribute),

    // Update an attribute
    updateAttribute: (id: string, attribute: UpdateAttributeRequest) =>
        apiClient.put<ApiResponse<AttributeDto>>(updateAttributeEndpoint(id), attribute),

    // Delete an attribute
    deleteAttribute: (id: string) =>
        apiClient.delete<ApiResponse<boolean>>(deleteAttributeEndpoint(id)),
};