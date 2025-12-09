import React from 'react';
import {ColumnDef} from '@tanstack/react-table';
import {Button} from '@/components/ui/button';
import {
  ArrowUpDown,
  ArrowUp,
  ArrowDown,
} from 'lucide-react';
import {CategoryDataTableDto, CategoryDto} from '@/models/catalog';
import {ServerDataTable} from '@/components/data-table/ServerDataTable';
import {CategoryActions} from './CategoryActions';
import {CategoryEmptyState} from './CategoryEmptyState';
import {CategoryStatusBadge} from './CategoryStatusBadge';
import {ParentCategoryBadge} from './ParentCategoryBadge';

interface CategoryTableProps {
  data: CategoryDataTableDto[];
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
  onEdit: (category: CategoryDto) => void;
  onDelete: (category: CategoryDto) => void;
  onToggleStatus: (category: CategoryDto, status: boolean) => void;
  onViewDetails: (category: CategoryDto) => void;
  onParentClick: (category: CategoryDto) => void;
  onAddNew: () => void;
}

export function CategoryTable({
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
                                onParentClick,
                                onAddNew,
                              }: CategoryTableProps) {
  // Column index mapping: 0: name, 1: slug, 2: parentId, 3: level, 4: isActive
  const columnIndexMap: Record<string, number> = {
    'name': 0,
    'slug': 1,
    'parentId': 2,
    'level': 3,
    'isActive': 4,
  };

  const handleColumnSort = (columnKey: string) => {
    const columnIndex = columnIndexMap[columnKey];
    if (columnIndex === undefined) return;

    const newDirection =
      sortColumn === columnIndex && sortDirection === 'asc' ? 'desc' : 'asc';

    onSort(columnIndex, newDirection);
  };

  const SortIcon = ({columnKey}: { columnKey: string }) => {
    const columnIndex = columnIndexMap[columnKey];
    const isActive = sortColumn === columnIndex;

    if (!isActive) {
      return <ArrowUpDown className="ml-2 h-4 w-4 text-muted-foreground"/>;
    }

    return sortDirection === 'asc'
      ? <ArrowUp className="ml-2 h-4 w-4"/>
      : <ArrowDown className="ml-2 h-4 w-4"/>;
  };

  const columns = React.useMemo<ColumnDef<CategoryDataTableDto>[]>(
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
            <SortIcon columnKey="name"/>
          </Button>
        ),
      },
      {
        accessorKey: 'slug',
        header: () => (
          <Button
            variant="ghost"
            onClick={() => handleColumnSort('slug')}
            className="-ml-4 hover:bg-transparent"
          >
            Slug
            <SortIcon columnKey="slug"/>
          </Button>
        ),
        cell: ({row}) => (
          <code className="text-xs bg-muted px-1.5 py-0.5 rounded">
            {row.original.slug}
          </code>
        ),
      },
      {
        accessorKey: 'parentId',
        header: () => (
          <Button
            variant="ghost"
            onClick={() => handleColumnSort('parentId')}
            className="-ml-4 hover:bg-transparent"
          >
            Parent
            <SortIcon columnKey="parentId"/>
          </Button>
        ),
        cell: ({row}) => (
          <ParentCategoryBadge
            parentName={row.original.parentName}
            category={row.original as any}
            onParentClick={onParentClick}
          />
        ),
      },
      {
        accessorKey: 'level',
        header: () => (
          <Button
            variant="ghost"
            onClick={() => handleColumnSort('level')}
            className="-ml-4 hover:bg-transparent"
          >
            Level
            <SortIcon columnKey="level"/>
          </Button>
        ),
        cell: ({row}) => (
          <div className="text-center">
            {row.original.level}
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
            <SortIcon columnKey="isActive"/>
          </Button>
        ),
        cell: ({row}) => (
          <CategoryStatusBadge isActive={row.original.isActive}/>
        ),
      },
      {
        id: 'actions',
        header: 'Actions',
        cell: ({row}) => (
          <CategoryActions
            category={row.original}
            onEdit={onEdit}
            onDelete={onDelete}
            onToggleStatus={onToggleStatus}
            onViewDetails={onViewDetails}
          />
        ),
      },
    ],
    [sortColumn, sortDirection, onEdit, onDelete, onToggleStatus, onViewDetails, onParentClick]
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
      searchPlaceholder="Search categories..."
      emptyState={<CategoryEmptyState onAddNew={onAddNew}/>}
      onAddNew={onAddNew}
      addNewLabel="Add Category"
    />
  );
}
