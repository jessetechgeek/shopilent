import {DataTableRequest} from './common';

// User Role Enum (based on API schema)
export enum UserRole {
  Admin = 0,
  Manager = 1,
  Customer = 2
}

// User Datatable DTO (based on OpenAPI UserDatatableDto)
export interface UserDatatableDto {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  phone?: string;
  role: UserRole;
  roleName: string;
  isActive: boolean;
  emailVerified?: boolean;
  lastLogin?: string;
  createdAt: string;
  updatedAt: string;
}

// Customer Datatable Request
export type UserDatatableRequest = DataTableRequest;

// Update User Status Request
export interface UpdateUserStatusRequest {
  isActive: boolean;
}

// Update User Role Request
export interface UpdateUserRoleRequest {
  role: UserRole;
}

// Update User Request (for editing customer details)
export interface UpdateUserRequest {
  firstName?: string;
  lastName?: string;
  phone?: string;
  email?: string;
}
