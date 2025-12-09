import apiClient, { ApiResponse } from './config';
import {
    UserDatatableDto,
    UserDatatableRequest,
    UpdateUserStatusRequest,
    UpdateUserRoleRequest
} from '@/models/customers';
import { DataTableResult } from '@/models/common';
import {
    UserEndpoint,
    getUserEndpoint,
    updateUserEndpoint,
    updateUserStatusEndpoint,
    updateUserRoleEndpoint
} from '@/api/endpoints';

export const customerApi = {
    // Get customers for datatable (with filtering, sorting, pagination)
    getCustomersForDatatable: (request: UserDatatableRequest) =>
        apiClient.post<ApiResponse<DataTableResult<UserDatatableDto>>>(UserEndpoint.Datatable, request),

    // Update customer status
    updateCustomerStatus: (id: string, request: UpdateUserStatusRequest) =>
        apiClient.put<ApiResponse<string>>(updateUserStatusEndpoint(id), request),

    // Update customer role
    updateCustomerRole: (id: string, request: UpdateUserRoleRequest) =>
        apiClient.put<ApiResponse<string>>(updateUserRoleEndpoint(id), request),

    // Get customer by ID
    getCustomerById: (id: string) =>
        apiClient.get<ApiResponse<UserDatatableDto>>(getUserEndpoint(id)),

    // Update customer details
    updateCustomer: (id: string, request: Record<string, unknown>) =>
        apiClient.put<ApiResponse<UserDatatableDto>>(updateUserEndpoint(id), request),
};