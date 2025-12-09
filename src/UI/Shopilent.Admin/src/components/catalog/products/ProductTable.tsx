// src/components/catalog/products/ProductTable.tsx
import React from 'react';
import { ColumnDef } from '@tanstack/react-table';
import { Button } from '@/components/ui/button';
import {
    ArrowUpDown,
    ArrowUp,
    ArrowDown,
    ImageIcon,
} from 'lucide-react';
import { ProductDataTableDto, ProductDto } from '@/models/catalog';
import { ServerDataTable } from '@/components/data-table/ServerDataTable';
import { ProductActions } from './ProductActions';
import { ProductEmptyState } from './ProductEmptyState';
import { ProductStatusBadge } from './ProductStatusBadge';
import { CategoryBadges } from './CategoryBadges';
import { StockIndicator } from './StockIndicator';
import { SkuBadge } from './SkuBadge';
import { PriceFormatter } from './PriceFormatter';

interface ProductTableProps {
    data: ProductDataTableDto[];
    totalRecords: number;
    filteredRecords: number;
    pageIndex: number;
    pageSize: number;
    sortColumn: number;
    sortDirection: 'asc' | 'desc';
    searchQuery: string;
    onPageChange: (page: number) => void;
    onPageSizeChange: (size: number) => void;
    onSearch: (query: string) => void;
    onSort: (column: number, direction: 'asc' | 'desc') => void;
    onEdit: (product: ProductDto) => void;
    onDelete: (product: ProductDto) => void;
    onToggleStatus: (product: ProductDto, status: boolean) => void;
    onViewDetails: (product: ProductDto) => void;
    onAddNew: () => void;
}

export function ProductTable({
    data,
    totalRecords,
    filteredRecords,
    pageIndex,
    pageSize,
    sortColumn,
    sortDirection,
    searchQuery,
    onPageChange,
    onPageSizeChange,
    onSearch,
    onSort,
    onEdit,
    onDelete,
    onToggleStatus,
    onViewDetails,
    onAddNew,
}: ProductTableProps) {
    // Column index mapping: 0: image, 1: name, 2: sku, 3: basePrice, 4: categories, 5: totalStock, 6: isActive
    const columnIndexMap: Record<string, number> = {
        'name': 1,
        'sku': 2,
        'basePrice': 3,
        'totalStockQuantity': 5,
        'isActive': 6,
    };

    const handleColumnSort = (columnKey: string) => {
        const columnIndex = columnIndexMap[columnKey];
        if (columnIndex === undefined) return;

        const newDirection =
            sortColumn === columnIndex && sortDirection === 'asc' ? 'desc' : 'asc';

        onSort(columnIndex, newDirection);
    };

    const SortIcon = ({ columnKey }: { columnKey: string }) => {
        const columnIndex = columnIndexMap[columnKey];
        const isActive = sortColumn === columnIndex;

        if (!isActive) {
            return <ArrowUpDown className="ml-2 h-4 w-4 text-muted-foreground" />;
        }

        return sortDirection === 'asc'
            ? <ArrowUp className="ml-2 h-4 w-4" />
            : <ArrowDown className="ml-2 h-4 w-4" />;
    };

    const columns = React.useMemo<ColumnDef<ProductDataTableDto>[]>(
        () => [
            {
                id: 'image',
                header: 'Image',
                cell: ({ row }) => {
                    const images = row.original.images;
                    let imageUrl: string | null = null;

                    // Handle different image formats
                    if (images && typeof images === 'object') {
                        if (Array.isArray(images) && images.length > 0) {
                            // If images is an array, get the first/default image
                            const defaultImage = images.find((img: any) => img.isDefault) || images[0];
                            // Prefer thumbnailUrl for better performance in table view
                            imageUrl = defaultImage?.thumbnailUrl || defaultImage?.imageUrl;
                        } else if (images.thumbnailUrl || images.imageUrl) {
                            // If images is a single object with url
                            imageUrl = images.thumbnailUrl || images.imageUrl;
                        }
                    }

                    return (
                        <div className="flex items-center justify-center w-16 h-16">
                            {imageUrl ? (
                                <img
                                    src={imageUrl}
                                    alt={row.original.name}
                                    className="w-full h-full object-cover rounded border"
                                />
                            ) : (
                                <div className="w-full h-full flex items-center justify-center bg-muted rounded border">
                                    <ImageIcon className="h-6 w-6 text-muted-foreground" />
                                </div>
                            )}
                        </div>
                    );
                },
            },
            {
                accessorKey: 'name',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('name')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Name
                        <SortIcon columnKey="name" />
                    </Button>
                ),
            },
            {
                accessorKey: 'sku',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('sku')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        SKU
                        <SortIcon columnKey="sku" />
                    </Button>
                ),
                cell: ({ row }) => <SkuBadge sku={row.original.sku} />,
            },
            {
                accessorKey: 'basePrice',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('basePrice')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Price
                        <SortIcon columnKey="basePrice" />
                    </Button>
                ),
                cell: ({ row }) => (
                    <div className="text-right">
                        <PriceFormatter price={row.original.basePrice} currency={row.original.currency} />
                    </div>
                ),
            },
            {
                accessorKey: 'categories',
                header: 'Categories',
                cell: ({ row }) => (
                    <div className="hidden lg:table-cell">
                        <CategoryBadges categories={row.original.categories || []} />
                    </div>
                ),
            },
            {
                accessorKey: 'totalStockQuantity',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('totalStockQuantity')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Stock
                        <SortIcon columnKey="totalStockQuantity" />
                    </Button>
                ),
                cell: ({ row }) => (
                    <div className="text-center">
                        <StockIndicator stockQuantity={row.original.totalStockQuantity ?? 0} />
                    </div>
                ),
            },
            {
                accessorKey: 'isActive',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('isActive')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Status
                        <SortIcon columnKey="isActive" />
                    </Button>
                ),
                cell: ({ row }) => (
                    <div className="text-center">
                        <ProductStatusBadge isActive={row.original.isActive} />
                    </div>
                ),
            },
            {
                id: 'actions',
                header: 'Actions',
                cell: ({ row }) => (
                    <ProductActions
                        product={row.original}
                        onEdit={onEdit}
                        onDelete={onDelete}
                        onToggleStatus={onToggleStatus}
                        onViewDetails={onViewDetails}
                    />
                ),
            },
        ],
        [sortColumn, sortDirection, onEdit, onDelete, onToggleStatus, onViewDetails]
    );

    return (
        <ServerDataTable
            data={data}
            columns={columns}
            totalRecords={totalRecords}
            filteredRecords={filteredRecords}
            pageIndex={pageIndex}
            pageSize={pageSize}
            searchQuery={searchQuery}
            onPageChange={onPageChange}
            onPageSizeChange={onPageSizeChange}
            onSearch={onSearch}
            searchPlaceholder="Search products..."
            emptyState={<ProductEmptyState onAddNew={onAddNew} />}
            onAddNew={onAddNew}
            addNewLabel="Add Product"
        />
    );
}
