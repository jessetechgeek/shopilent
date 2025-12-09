// src/components/customers/CustomerTable.tsx
import React from 'react';
import { ColumnDef } from '@tanstack/react-table';
import { Button } from '@/components/ui/button';
import {
    ArrowUpDown,
    ArrowUp,
    ArrowDown,
} from 'lucide-react';
import { UserDatatableDto } from '@/models/customers';
import { ServerDataTable } from '@/components/data-table/ServerDataTable';
import { CustomerActions } from './CustomerActions';
import { CustomerEmptyState } from './CustomerEmptyState';
import { CustomerStatusBadge } from './CustomerStatusBadge';
import { CustomerRoleBadge } from './CustomerRoleBadge';

interface CustomerTableProps {
    data: UserDatatableDto[];
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
    onViewDetails: (customer: UserDatatableDto) => void;
    onUpdateStatus: (customer: UserDatatableDto) => void;
    onUpdateRole: (customer: UserDatatableDto) => void;
    onEdit: (customer: UserDatatableDto) => void;
}

export function CustomerTable({
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
    onViewDetails,
    onUpdateStatus,
    onUpdateRole,
    onEdit,
}: CustomerTableProps) {
    // Column index mapping to match server-side DataTables format
    // 0: id, 1: fullName, 2: email, 3: phone, 4: role, 5: isActive, 6: createdAt
    const columnIndexMap: Record<string, number> = {
        'id': 0,
        'fullName': 1,
        'phone': 3,
        'role': 4,
        'isActive': 5,
        'createdAt': 6,
    };

    // Format date for display
    const formatDate = (dateString: string) => {
        return new Date(dateString).toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    };

    // Handle column sort
    const handleColumnSort = (columnKey: string) => {
        const columnIndex = columnIndexMap[columnKey];
        if (columnIndex === undefined) return;

        // Toggle direction if same column, otherwise default to asc
        const newDirection =
            sortColumn === columnIndex && sortDirection === 'asc'
                ? 'desc'
                : 'asc';

        onSort(columnIndex, newDirection);
    };

    // Render sort icon
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

    // Define columns
    const columns = React.useMemo<ColumnDef<UserDatatableDto>[]>(
        () => [
            {
                accessorKey: 'id',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('id')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Customer ID
                        <SortIcon columnKey="id" />
                    </Button>
                ),
                cell: ({ row }) => (
                    <div className="font-mono text-sm">
                        #{row.original.id.slice(-8).toUpperCase()}
                    </div>
                ),
            },
            {
                accessorKey: 'fullName',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('fullName')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Name
                        <SortIcon columnKey="fullName" />
                    </Button>
                ),
                cell: ({ row }) => (
                    <div>
                        <div className="font-medium">{row.original.fullName}</div>
                        <div className="text-sm text-muted-foreground">{row.original.email}</div>
                    </div>
                ),
            },
            {
                accessorKey: 'phone',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('phone')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Phone
                        <SortIcon columnKey="phone" />
                    </Button>
                ),
                cell: ({ row }) => (
                    <div className="text-sm">
                        {row.original.phone || 'N/A'}
                    </div>
                ),
            },
            {
                accessorKey: 'role',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('role')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Role
                        <SortIcon columnKey="role" />
                    </Button>
                ),
                cell: ({ row }) => (
                    <CustomerRoleBadge role={row.original.role} roleName={row.original.roleName} />
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
                    <CustomerStatusBadge isActive={row.original.isActive} />
                ),
            },
            {
                accessorKey: 'createdAt',
                header: () => (
                    <Button
                        variant="ghost"
                        onClick={() => handleColumnSort('createdAt')}
                        className="-ml-4 hover:bg-transparent"
                    >
                        Join Date
                        <SortIcon columnKey="createdAt" />
                    </Button>
                ),
                cell: ({ row }) => (
                    <div className="text-sm">
                        {formatDate(row.original.createdAt)}
                    </div>
                ),
            },
            {
                id: 'actions',
                header: 'Actions',
                cell: ({ row }) => (
                    <div className="min-w-[160px]">
                        <CustomerActions
                            customer={row.original}
                            onViewDetails={onViewDetails}
                            onUpdateStatus={onUpdateStatus}
                            onUpdateRole={onUpdateRole}
                            onEdit={onEdit}
                        />
                    </div>
                ),
            },
        ],
        [sortColumn, sortDirection, onViewDetails, onUpdateStatus, onUpdateRole, onEdit]
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
            searchPlaceholder="Search customers..."
            emptyState={<CustomerEmptyState />}
        />
    );
}
