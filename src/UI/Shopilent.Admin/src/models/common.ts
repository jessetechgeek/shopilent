// DataTable request interface
export interface DataTableRequest {
  draw: number;
  start: number;
  length: number;
  columns: DataTableColumn[];
  order: DataTableOrder[];
  search: DataTableSearch;
}

export interface DataTableColumn {
  data: string;
  name: string;
  searchable: boolean;
  orderable: boolean;
  search: DataTableSearch;
}

export interface DataTableOrder {
  column: number;
  dir: 'asc' | 'desc';
}

export interface DataTableSearch {
  value: string;
  regex: boolean;
}

// Generic DataTable response interface
export interface DataTableResult<T> {
  draw: number;
  recordsTotal: number;
  recordsFiltered: number;
  data: T[];
  error?: string;
}

// Generic paginated result interface
export interface PaginatedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}
