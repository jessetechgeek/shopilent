import {DataTableRequest} from './common';

// Category DTOs and Requests
export interface CategoryDto {
  id: string;
  name: string;
  description?: string;
  parentId?: string;
  slug: string;
  level: number;
  path: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CategoryDataTableDto {
  id: string;
  name: string;
  description?: string;
  parentId?: string;
  parentName?: string;
  slug: string;
  level: number;
  path: string;
  productCount: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCategoryRequest {
  name: string;
  slug: string;
  description?: string;
  parentId?: string;
  isActive?: boolean;
}

export interface UpdateCategoryRequest {
  name: string;
  slug: string;
  description?: string;
  isActive?: boolean;
}

export interface UpdateCategoryStatusRequest {
  isActive: boolean;
}

export interface UpdateCategoryParentRequest {
  parentId?: string;
}

export interface CategoryDatatableRequest extends DataTableRequest {
}

// Updated AttributeType enum to use numeric values
export enum AttributeType {
  Text = 'Text',
  Number = 'Number',
  Boolean = 'Boolean',
  Select = 'Select',
  Color = 'Color',
  Date = 'Date',
  Dimensions = 'Dimensions',
  Weight = 'Weight'
}

// export interface AttributeDto {
//     id: string;
//     name: string;
//     displayName: string;
//     type: AttributeType;
//     configuration: Record<string, any>;
//     filterable: boolean;
//     searchable: boolean;
//     isVariant: boolean;
//     createdAt: string;
//     updatedAt: string;
// }
//
// export interface AttributeDataTableDto extends AttributeDto {
//     // Any additional fields for the datatable view
//     usageCount?: number;
// }
//
// export interface CreateAttributeRequest {
//     name: string;
//     displayName: string;
//     type: AttributeType;
//     configuration?: Record<string, any>;
//     filterable?: boolean;
//     searchable?: boolean;
//     isVariant?: boolean;
// }
//
// export interface UpdateAttributeRequest {
//     displayName: string;
//     configuration?: Record<string, any>;
//     filterable?: boolean;
//     searchable?: boolean;
//     isVariant?: boolean;
// }
//
// export interface AttributeDatatableRequest extends DataTableRequest {
// }
export interface AttributeDto {
  id: string;
  name: string;
  displayName: string;
  type: AttributeType;
  configuration: Record<string, any>;
  filterable: boolean;
  searchable: boolean;
  isVariant: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface AttributeDataTableDto extends AttributeDto {
  // Any additional fields for the datatable view
  usageCount?: number;
}

export interface CreateAttributeRequest {
  name: string;
  displayName: string;
  type: AttributeType;
  configuration?: Record<string, any>;
  filterable?: boolean;
  searchable?: boolean;
  isVariant?: boolean;
}

export interface UpdateAttributeRequest {
  displayName: string;
  configuration?: Record<string, any>;
  filterable?: boolean;
  searchable?: boolean;
  isVariant?: boolean;
}

export interface AttributeDatatableRequest extends DataTableRequest {
}

export interface ProductDto {
  id: string;
  name: string;
  description?: string;
  basePrice: number;
  currency: string;
  sku?: string;
  slug: string;
  isActive: boolean;
  metadata: Record<string, any>;
  images: any;
  createdAt: string;
  updatedAt: string;
}

export interface ProductDataTableDto extends ProductDto {
  categoryNames: string[];
  categories?: string[]; // Add this
  inStock: boolean;
  totalStock: number;
  totalStockQuantity?: number; // Add this alias
  variantsCount: number;
}

export interface UpdateProductStatusRequest {
  isActive: boolean;
}

export interface ProductDatatableRequest extends DataTableRequest {
}

export interface ProductDetailDto extends ProductDto {
  categories: CategoryDto[];
  attributes: ProductAttributeDto[];
  variants: ProductVariantDto[];
  createdBy?: string;
  modifiedBy?: string;
  lastModified?: string;
}

export interface ProductAttributeDto {
  id: string;
  productId: string;
  attributeId: string;
  attributeName: string;
  attributeDisplayName: string;
  values: Record<string, any>;
  createdAt: string;
  updatedAt: string;
}

export interface CreateProductRequest {
  name: string;
  description?: string;
  basePrice: number;
  currency: string;
  sku?: string;
  slug: string;
  categoryIds?: string[];
  isActive?: boolean;
  attributes?: { attributeId: string; value: any }[];
  metadata?: Record<string, any>;
}

export interface UpdateProductRequest {
  name: string;
  description?: string;
  basePrice: number;
  slug: string;
  sku?: string;
  isActive?: boolean;
  categoryIds?: string[];
  attributes?: { attributeId: string; value: any }[];
  removeExistingImages?: boolean;
  imagesToRemove?: string[];
  imageOrders?: ProductImageOrder[]; // Add this new field
}

export interface ProductVariantDto {
  id: string;
  productId: string;
  sku?: string;
  price: number;
  currency: string;
  stockQuantity: number;
  isActive: boolean;
  metadata: Record<string, any>;
  attributes: VariantAttributeDto[];
  // Add images field
  images?: VariantImageDto[];
  createdAt: string;
  updatedAt: string;
}

export interface VariantAttributeDto {
  variantId: string;
  attributeId: string;
  attributeName: string;
  attributeDisplayName: string;
  value: any;
}

export interface CreateProductVariantRequest {
  productId: string;
  sku?: string;
  price: number;
  stockQuantity: number;
  isActive?: boolean;
  attributes: { attributeId: string; value: any }[];
  metadata?: Record<string, any>;
  // Add image support
  images?: Array<{
    file?: File;
    altText?: string;
    isDefault?: boolean;
    displayOrder?: number;
  }>;
  file?: File[]; // For FormData compatibility with backend
}

export interface UpdateProductVariantRequest {
  sku?: string;
  price: number;
  stockQuantity: number;
  isActive?: boolean;
  attributes: { attributeId: string; value: any }[];
  metadata?: Record<string, any>;
  // Add image support
  images?: Array<{
    file?: File;
    altText?: string;
    isDefault?: boolean;
    displayOrder?: number;
    imageKey?: string; // For existing images
  }>;
  file?: File[]; // For FormData compatibility with backend
  imagesToRemove?: string[]; // Track removed images
  imageOrders?: VariantImageOrder[]; // Add imageOrders support for variants
}

export interface UpdateVariantStockRequest {
  quantity: number;
}

export interface UpdateVariantStatusRequest {
  isActive: boolean;
}

export interface UpdateVariantAttributesRequest {
  attributes: { attributeId: string; value: any }[];
}

export interface VariantDatatableRequest extends DataTableRequest {
}

export interface VariantImageDto {
  id: string;
  variantId: string;
  imageKey: string;
  thumbnailKey: string;
  imageUrl: string;
  thumbnailUrl: string;
  url: string;
  altText?: string;
  isDefault: boolean;
  displayOrder: number;
  createdAt: string;
  updatedAt: string;
}

export interface ProductImageOrder {
  imageKey: string;
  displayOrder: number;
  isDefault: boolean;
}

export interface ProductImageDto {
  id: string;
  productId: string;
  imageKey: string;
  thumbnailKey: string;
  imageUrl: string;
  thumbnailUrl: string;
  url: string;
  altText?: string;
  isDefault: boolean;
  displayOrder: number;
  createdAt: string;
  updatedAt: string;
}

export interface VariantImageOrder {
  imageKey: string;
  displayOrder: number;
  isDefault: boolean;
}

export interface UpdateProductCategoriesRequest {
  categoryIds: string[];
}

export interface UpdateProductAttributesRequest {
  attributes: { attributeId: string; value: any }[];
}
