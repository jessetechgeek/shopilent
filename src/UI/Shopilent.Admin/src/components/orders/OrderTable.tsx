import React from 'react';
import {ColumnDef} from '@tanstack/react-table';
import {Button} from '@/components/ui/button';
import {
  ArrowUpDown,
  ArrowUp,
  ArrowDown,
} from 'lucide-react';
import {OrderDataTableDto, OrderDto} from '@/models/orders';
import {ServerDataTable} from '@/components/data-table/ServerDataTable';
import {OrderActions} from './OrderActions';
import {OrderEmptyState} from './OrderEmptyState';
import {OrderStatusBadge} from './OrderStatusBadge';
import {PaymentStatusBadge} from './PaymentStatusBadge';
import {PriceFormatter} from './PriceFormatter';

interface OrderTableProps {
  data: OrderDataTableDto[];
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
  onViewDetails: (order: OrderDto) => void;
  onUpdateStatus: (order: OrderDto) => void;
  onUpdatePayment: (order: OrderDto) => void;
  onUpdateTracking: (order: OrderDto) => void;
  onMarkAsShipped: (order: OrderDto) => void;
  onMarkAsDelivered: (order: OrderDto) => void;
  onMarkAsReturned: (order: OrderDto) => void;
  onRefund: (order: OrderDto) => void;
  onPartialRefund: (order: OrderDto) => void;
  onCancel: (order: OrderDto) => void;
}

export function OrderTable({
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
                             onUpdatePayment,
                             onUpdateTracking,
                             onMarkAsShipped,
                             onMarkAsDelivered,
                             onMarkAsReturned,
                             onRefund,
                             onPartialRefund,
                             onCancel,
                           }: OrderTableProps) {
  // Column index mapping to match server-side DataTables format
  // 0: id, 1: userFullName, 2: userEmail, 3: total, 4: status, 5: paymentStatus, 6: itemsCount, 7: createdAt
  const columnIndexMap: Record<string, number> = {
    'id': 0,
    'userFullName': 1,
    'total': 3,
    'status': 4,
    'paymentStatus': 5,
    'itemsCount': 6,
    'createdAt': 7,
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

    const newDirection =
      sortColumn === columnIndex && sortDirection === 'asc'
        ? 'desc'
        : 'asc';

    onSort(columnIndex, newDirection);
  };

  // Render sort icon
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

  // Define columns
  const columns = React.useMemo<ColumnDef<OrderDataTableDto>[]>(
    () => [
      {
        accessorKey: 'id',
        header: () => (
          <Button
            variant="ghost"
            onClick={() => handleColumnSort('id')}
            className="-ml-4 hover:bg-transparent"
          >
            Order ID
            <SortIcon columnKey="id"/>
          </Button>
        ),
        cell: ({row}) => (
          <div className="font-mono text-sm">
            #{row.original.id.slice(-8).toUpperCase()}
          </div>
        ),
      },
      {
        accessorKey: 'userFullName',
        header: () => (
          <Button
            variant="ghost"
            onClick={() => handleColumnSort('userFullName')}
            className="-ml-4 hover:bg-transparent"
          >
            Customer
            <SortIcon columnKey="userFullName"/>
          </Button>
        ),
        cell: ({row}) => (
          <div>
            <div className="font-medium">{row.original.userFullName}</div>
            <div className="text-sm text-muted-foreground">{row.original.userEmail}</div>
          </div>
        ),
      },
      {
        accessorKey: 'total',
        header: () => (
          <Button
            variant="ghost"
            onClick={() => handleColumnSort('total')}
            className="-ml-4 hover:bg-transparent"
          >
            Total
            <SortIcon columnKey="total"/>
          </Button>
        ),
        cell: ({row}) => (
          <PriceFormatter
            amount={row.original.total}
            currency={row.original.currency}
            className="font-medium"
          />
        ),
      },
      {
        accessorKey: 'status',
        header: () => (
          <Button
            variant="ghost"
            onClick={() => handleColumnSort('status')}
            className="-ml-4 hover:bg-transparent"
          >
            Order Status
            <SortIcon columnKey="status"/>
          </Button>
        ),
        cell: ({row}) => (
          <OrderStatusBadge status={row.original.status}/>
        ),
      },
      {
        accessorKey: 'paymentStatus',
        header: () => (
          <Button
            variant="ghost"
            onClick={() => handleColumnSort('paymentStatus')}
            className="-ml-4 hover:bg-transparent"
          >
            Payment
            <SortIcon columnKey="paymentStatus"/>
          </Button>
        ),
        cell: ({row}) => (
          <PaymentStatusBadge status={row.original.paymentStatus}/>
        ),
      },
      {
        accessorKey: 'itemsCount',
        header: () => (
          <Button
            variant="ghost"
            onClick={() => handleColumnSort('itemsCount')}
            className="-ml-4 hover:bg-transparent"
          >
            Items
            <SortIcon columnKey="itemsCount"/>
          </Button>
        ),
        cell: ({row}) => (
          <div className="text-center">
            {row.original.itemsCount}
          </div>
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
            Order Date
            <SortIcon columnKey="createdAt"/>
          </Button>
        ),
        cell: ({row}) => (
          <div className="text-sm">
            {formatDate(row.original.createdAt)}
          </div>
        ),
      },
      {
        id: 'actions',
        header: '',
        cell: ({row}) => (
          <OrderActions
            order={row.original}
            onViewDetails={onViewDetails}
            onUpdateStatus={onUpdateStatus}
            onUpdatePayment={onUpdatePayment}
            onUpdateTracking={onUpdateTracking}
            onMarkAsShipped={onMarkAsShipped}
            onMarkAsDelivered={onMarkAsDelivered}
            onMarkAsReturned={onMarkAsReturned}
            onRefund={onRefund}
            onPartialRefund={onPartialRefund}
            onCancel={onCancel}
          />
        ),
      },
    ],
    [sortColumn, sortDirection, onViewDetails, onUpdateStatus, onUpdatePayment, onUpdateTracking, onMarkAsShipped, onMarkAsDelivered, onMarkAsReturned, onRefund, onPartialRefund, onCancel]
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
      searchPlaceholder="Search orders..."
      emptyState={<OrderEmptyState/>}
    />
  );
}
