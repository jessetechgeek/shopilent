import React from 'react';
import {
    useReactTable,
    getCoreRowModel,
    ColumnDef,
    flexRender,
} from '@tanstack/react-table';
import {
    Table,
    TableHeader,
    TableRow,
    TableHead,
    TableBody,
    TableCell,
} from '@/components/ui/table';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
    ChevronLeft,
    ChevronRight,
    ChevronsLeft,
    ChevronsRight,
    Search,
    Plus,
} from 'lucide-react';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';

interface ServerDataTableProps<T> {
    data: T[];
    columns: ColumnDef<T>[];
    totalRecords: number;
    filteredRecords: number;
    pageIndex: number;
    pageSize: number;
    searchQuery: string;
    onPageChange: (page: number) => void;
    onPageSizeChange: (size: number) => void;
    onSearch: (query: string) => void;
    searchPlaceholder?: string;
    emptyState?: React.ReactNode;
    pageSizeOptions?: number[];
    onAddNew?: () => void;
    addNewLabel?: string;
}

export function ServerDataTable<T>({
    data,
    columns,
    totalRecords,
    filteredRecords,
    pageIndex,
    pageSize,
    searchQuery,
    onPageChange,
    onPageSizeChange,
    onSearch,
    searchPlaceholder = 'Search...',
    emptyState,
    pageSizeOptions = [10, 20, 30, 40, 50],
    onAddNew,
    addNewLabel = 'Add New',
}: ServerDataTableProps<T>) {
    const [inputValue, setInputValue] = React.useState(searchQuery);
    const searchTimeoutRef = React.useRef<NodeJS.Timeout | null>(null);

    // Sync input value with parent searchQuery only when parent changes externally (e.g., clear button)
    React.useEffect(() => {
        setInputValue(searchQuery);
    }, [searchQuery]);

    // Create table instance with server-side operations
    const table = useReactTable({
        data,
        columns,
        getCoreRowModel: getCoreRowModel(),
        manualPagination: true,
        manualSorting: true,
        manualFiltering: true,
        pageCount: filteredRecords > 0 ? Math.ceil(filteredRecords / pageSize) : 0,
        state: {
            pagination: {
                pageIndex,
                pageSize,
            },
        },
    });

    // Handle search with debounce (500ms)
    const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const value = e.target.value;

        // Update local input value immediately
        setInputValue(value);

        // Clear existing timeout
        if (searchTimeoutRef.current) {
            clearTimeout(searchTimeoutRef.current);
        }

        // Set new timeout for server call
        searchTimeoutRef.current = setTimeout(() => {
            onSearch(value);
        }, 500);
    };

    // Cleanup timeout on unmount
    React.useEffect(() => {
        return () => {
            if (searchTimeoutRef.current) {
                clearTimeout(searchTimeoutRef.current);
            }
        };
    }, []);

    // Calculate page count from backend filteredRecords (uses filtered count when searching, total when not)
    const pageCount = filteredRecords > 0 ? Math.ceil(filteredRecords / pageSize) : 0;

    return (
        <div className="space-y-4">
            {/* Search and Add New Button */}
            <div className="flex items-center justify-between gap-2">
                <div className="relative flex-1 max-w-sm">
                    <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                    <Input
                        placeholder={searchPlaceholder}
                        value={inputValue}
                        onChange={handleSearchChange}
                        className="pl-9"
                    />
                </div>
                {onAddNew && (
                    <Button onClick={onAddNew} className="shrink-0 ml-auto">
                        <Plus className="size-4 mr-2" />
                        {addNewLabel}
                    </Button>
                )}
            </div>

            {/* Table */}
            <div className="rounded-md border">
                <Table>
                    <TableHeader>
                        {table.getHeaderGroups().map((headerGroup) => (
                            <TableRow key={headerGroup.id}>
                                {headerGroup.headers.map((header) => (
                                    <TableHead key={header.id}>
                                        {header.isPlaceholder
                                            ? null
                                            : flexRender(
                                                  header.column.columnDef.header,
                                                  header.getContext()
                                              )}
                                    </TableHead>
                                ))}
                            </TableRow>
                        ))}
                    </TableHeader>
                    <TableBody>
                        {table.getRowModel().rows?.length ? (
                            table.getRowModel().rows.map((row) => (
                                <TableRow
                                    key={row.id}
                                    data-state={row.getIsSelected() && 'selected'}
                                >
                                    {row.getVisibleCells().map((cell) => (
                                        <TableCell key={cell.id}>
                                            {flexRender(
                                                cell.column.columnDef.cell,
                                                cell.getContext()
                                            )}
                                        </TableCell>
                                    ))}
                                </TableRow>
                            ))
                        ) : (
                            <TableRow>
                                <TableCell colSpan={columns.length} className="h-24">
                                    {emptyState || (
                                        <div className="text-center py-10">
                                            <p className="text-muted-foreground">No data found</p>
                                        </div>
                                    )}
                                </TableCell>
                            </TableRow>
                        )}
                    </TableBody>
                </Table>
            </div>

            {/* Pagination */}
            <div className="flex items-center justify-between px-2">
                <div className="flex items-center space-x-6">
                    <div className="flex items-center space-x-2">
                        <p className="text-sm font-medium">Rows per page</p>
                        <Select
                            value={`${pageSize}`}
                            onValueChange={(value) => {
                                onPageSizeChange(Number(value));
                            }}
                        >
                            <SelectTrigger className="h-8 w-[70px]">
                                <SelectValue placeholder={pageSize} />
                            </SelectTrigger>
                            <SelectContent side="top">
                                {pageSizeOptions.map((size) => (
                                    <SelectItem key={size} value={`${size}`}>
                                        {size}
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                    </div>
                    <div className="text-sm text-muted-foreground">
                        Showing {data.length === 0 ? 0 : pageIndex * pageSize + 1} to {Math.min((pageIndex + 1) * pageSize, filteredRecords)} of {filteredRecords} entries
                        {searchQuery && filteredRecords !== totalRecords && (
                            <span className="text-muted-foreground/70"> (filtered from {totalRecords} total)</span>
                        )}
                    </div>
                </div>

                <div className="flex items-center space-x-6 lg:space-x-8">
                    <div className="flex items-center justify-center text-sm font-medium">
                        Page {pageCount === 0 ? 0 : pageIndex + 1} of {pageCount}
                    </div>
                    <div className="flex items-center space-x-2">
                        <Button
                            variant="outline"
                            className="hidden h-8 w-8 p-0 lg:flex"
                            onClick={() => onPageChange(0)}
                            disabled={pageIndex === 0}
                        >
                            <span className="sr-only">Go to first page</span>
                            <ChevronsLeft className="h-4 w-4" />
                        </Button>
                        <Button
                            variant="outline"
                            className="h-8 w-8 p-0"
                            onClick={() => onPageChange(pageIndex - 1)}
                            disabled={pageIndex === 0}
                        >
                            <span className="sr-only">Go to previous page</span>
                            <ChevronLeft className="h-4 w-4" />
                        </Button>
                        <Button
                            variant="outline"
                            className="h-8 w-8 p-0"
                            onClick={() => onPageChange(pageIndex + 1)}
                            disabled={pageIndex >= pageCount - 1}
                        >
                            <span className="sr-only">Go to next page</span>
                            <ChevronRight className="h-4 w-4" />
                        </Button>
                        <Button
                            variant="outline"
                            className="hidden h-8 w-8 p-0 lg:flex"
                            onClick={() => onPageChange(pageCount - 1)}
                            disabled={pageIndex >= pageCount - 1}
                        >
                            <span className="sr-only">Go to last page</span>
                            <ChevronsRight className="h-4 w-4" />
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    );
}
