import React from 'react';
import { ColumnDef } from '@tanstack/react-table';
import { Button } from '@/components/ui/button';
import {
    ArrowUpDown,
    ArrowUp,
    ArrowDown,
} from 'lucide-react';
import { AttributeDataTableDto, AttributeDto } from '@/models/catalog';
import { ServerDataTable } from '@/components/data-table/ServerDataTable';
import { AttributeActions } from './AttributeActions';
import { AttributeEmptyState } from './AttributeEmptyState';
import { AttributeBadge } from './AttributeBadge';
import { BooleanIndicator } from '@/components/ui/boolean-indicator';

interface AttributeTableProps {
    data: AttributeDataTableDto[];
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
    onEdit: (attribute: AttributeDto) => void;
    onDelete: (attribute: AttributeDto) => void;
    onViewDetails: (attribute: AttributeDto) => void;
    onAddNew: () => void;
}

export function AttributeTable({
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
    onViewDetails,
    onAddNew,
}: AttributeTableProps) {
    // Column index mapping: 0: name, 1: displayName, 2: type, 3: isVariant, 4: filterable
    const columnIndexMap: Record<string, number> = {
        'name': 0,
        'displayName': 1,
        'type': 2,
        'isVariant': 3,
        'filterable': 4,
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

    const columns = React.useMemo<ColumnDef<AttributeDataTableDto>[]>(
        () => [
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
                cell: ({ row }) => (
                    <code className="text-xs bg-muted px-1.5 py-0.5 rounded">
                        {row.original.name}
                    </code>
                ),
            },
            {
                accessorKey: 'displayName',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('displayName')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Display Name
                        <SortIcon columnKey="displayName" />
                    </Button>
                ),
            },
            {
                accessorKey: 'type',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('type')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Type
                        <SortIcon columnKey="type" />
                    </Button>
                ),
                cell: ({ row }) => (
                    <AttributeBadge type={row.original.type} />
                ),
            },
            {
                accessorKey: 'isVariant',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('isVariant')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Variant
                        <SortIcon columnKey="isVariant" />
                    </Button>
                ),
                cell: ({ row }) => (
                    <div className="text-center">
                        <BooleanIndicator value={row.original.isVariant} />
                    </div>
                ),
            },
            {
                accessorKey: 'filterable',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('filterable')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Filterable
                        <SortIcon columnKey="filterable" />
                    </Button>
                ),
                cell: ({ row }) => (
                    <div className="text-center">
                        <BooleanIndicator value={row.original.filterable} />
                    </div>
                ),
            },
            {
                id: 'actions',
                header: 'Actions',
                cell: ({ row }) => (
                    <AttributeActions
                        attribute={row.original}
                        onEdit={onEdit}
                        onDelete={onDelete}
                        onViewDetails={onViewDetails}
                    />
                ),
            },
        ],
        [sortColumn, sortDirection, onEdit, onDelete, onViewDetails]
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
            searchPlaceholder="Search attributes..."
            emptyState={<AttributeEmptyState onAddNew={onAddNew} />}
            onAddNew={onAddNew}
            addNewLabel="Add Attribute"
        />
    );
}
