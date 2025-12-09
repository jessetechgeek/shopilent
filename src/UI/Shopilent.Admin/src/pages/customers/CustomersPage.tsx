import React, {useState, useCallback} from 'react';
import {useQuery, useMutation, useQueryClient} from '@tanstack/react-query';
import {Loader2, Users} from 'lucide-react';
import {customerApi} from '@/api/customers';
import {CustomerTable} from '@/components/customers/CustomerTable';
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogCancel,
  AlertDialogAction,
} from '@/components/ui/alert-dialog';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {toast} from '@/components/ui/use-toast';
import {
  UserDatatableDto,
  UserDatatableRequest,
  UpdateUserStatusRequest,
  UpdateUserRoleRequest,
  UserRole
} from '@/models/customers';
import {useTitle} from '@/hooks/useTitle';

const CustomersPage: React.FC = () => {
  useTitle('Customers');
  const queryClient = useQueryClient();

  // State for table
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const [sortColumn, setSortColumn] = useState(6); // createdAt column by default
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc');

  // State for dialogs
  const [statusDialogOpen, setStatusDialogOpen] = useState(false);
  const [roleDialogOpen, setRoleDialogOpen] = useState(false);
  const [detailsDialogOpen, setDetailsDialogOpen] = useState(false);
  const [selectedCustomer, setSelectedCustomer] = useState<UserDatatableDto | null>(null);

  // Fetch customers query
  const {data, error} = useQuery({
    queryKey: ['customers', pageIndex, pageSize, searchQuery, sortColumn, sortDirection],
    queryFn: async () => {
      const request: UserDatatableRequest = {
        draw: 1,
        start: pageIndex * pageSize,
        length: pageSize,
        search: {
          value: searchQuery,
          regex: false
        },
        columns: [
          {data: 'id', name: 'id', searchable: true, orderable: true, search: {value: '', regex: false}},
          {data: 'fullName', name: 'fullName', searchable: true, orderable: true, search: {value: '', regex: false}},
          {data: 'email', name: 'email', searchable: true, orderable: true, search: {value: '', regex: false}},
          {data: 'phone', name: 'phone', searchable: true, orderable: true, search: {value: '', regex: false}},
          {data: 'role', name: 'role', searchable: false, orderable: true, search: {value: '', regex: false}},
          {data: 'isActive', name: 'isActive', searchable: false, orderable: true, search: {value: '', regex: false}},
          {data: 'createdAt', name: 'createdAt', searchable: false, orderable: true, search: {value: '', regex: false}}
        ],
        order: [
          {column: sortColumn, dir: sortDirection}
        ]
      };

      const response = await customerApi.getCustomersForDatatable(request);
      if (response.data.succeeded) {
        return response.data.data;
      }
      throw new Error(response.data.message || 'Failed to fetch customers');
    },
    staleTime: 5 * 60 * 1000, // 5 minutes
    retry: 2,
  });

  // Handle query errors
  React.useEffect(() => {
    if (error) {
      toast({
        title: 'Error',
        description: error.message || 'Failed to fetch customers',
        variant: 'destructive'
      });
    }
  }, [error]);

  // Update customer status mutation
  const updateStatusMutation = useMutation({
    mutationFn: ({id, request}: { id: string; request: UpdateUserStatusRequest }) =>
      customerApi.updateCustomerStatus(id, request),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Customer status updated successfully'
      });
      queryClient.invalidateQueries({queryKey: ['customers']});
      setStatusDialogOpen(false);
      setSelectedCustomer(null);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update customer status',
        variant: 'destructive'
      });
    }
  });

  // Update customer role mutation
  const updateRoleMutation = useMutation({
    mutationFn: ({id, request}: { id: string; request: UpdateUserRoleRequest }) =>
      customerApi.updateCustomerRole(id, request),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Customer role updated successfully'
      });
      queryClient.invalidateQueries({queryKey: ['customers']});
      setRoleDialogOpen(false);
      setSelectedCustomer(null);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update customer role',
        variant: 'destructive'
      });
    }
  });

  // Handle search with useCallback to prevent unnecessary re-renders
  const handleSearch = useCallback((query: string) => {
    setSearchQuery(query);
    setPageIndex(0); // Reset to first page when searching
  }, []);

  // Handle sorting
  const handleSort = (column: number, direction: 'asc' | 'desc') => {
    setSortColumn(column);
    setSortDirection(direction);
  };

  // Handle view details
  const handleViewDetails = (customer: UserDatatableDto) => {
    setSelectedCustomer(customer);
    setDetailsDialogOpen(true);
  };

  // Handle update status
  const handleUpdateStatus = (customer: UserDatatableDto) => {
    setSelectedCustomer(customer);
    setStatusDialogOpen(true);
  };

  // Handle update role
  const handleUpdateRole = (customer: UserDatatableDto) => {
    setSelectedCustomer(customer);
    setRoleDialogOpen(true);
  };

  // Handle edit customer - now just a placeholder since we navigate to edit page
  const handleEdit = (customer: UserDatatableDto) => {
    // This is now handled by the CustomerActions component via navigation
    console.log('Edit customer:', customer.id);
  };

  // Handle confirm status update
  const handleConfirmStatusUpdate = () => {
    if (selectedCustomer) {
      const request: UpdateUserStatusRequest = {
        isActive: !selectedCustomer.isActive
      };
      updateStatusMutation.mutate({id: selectedCustomer.id, request});
    }
  };

  // Handle confirm role update
  const handleConfirmRoleUpdate = (roleName: string) => {
    if (selectedCustomer) {
      const roleValue = UserRole[roleName as keyof typeof UserRole];
      const request: UpdateUserRoleRequest = {role: roleValue};
      updateRoleMutation.mutate({id: selectedCustomer.id, request});
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Customers</h1>
          <p className="text-muted-foreground">
            Manage customer accounts and permissions
          </p>
        </div>
        <div className="flex items-center space-x-2">
          <Users className="h-5 w-5 text-muted-foreground"/>
          <span className="text-sm text-muted-foreground">
                        {data?.recordsTotal || 0} total customers
                    </span>
        </div>
      </div>

      {/* Customers Table */}
      <CustomerTable
        data={data?.data || []}
        totalRecords={data?.recordsTotal || 0}
        filteredRecords={data?.recordsFiltered || 0}
        pageIndex={pageIndex}
        pageSize={pageSize}
        sortColumn={sortColumn}
        sortDirection={sortDirection}
        searchQuery={searchQuery}
        onPageChange={setPageIndex}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setPageIndex(0); // Reset to first page when changing page size
        }}
        onSearch={handleSearch}
        onSort={handleSort}
        onViewDetails={handleViewDetails}
        onUpdateStatus={handleUpdateStatus}
        onUpdateRole={handleUpdateRole}
        onEdit={handleEdit}
      />

      {/* Customer Status Update Dialog */}
      <AlertDialog open={statusDialogOpen} onOpenChange={setStatusDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {selectedCustomer?.isActive ? 'Deactivate' : 'Activate'} Customer
            </AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want
              to {selectedCustomer?.isActive ? 'deactivate' : 'activate'} {selectedCustomer?.fullName}?
              {selectedCustomer?.isActive && ' This will prevent them from accessing their account.'}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmStatusUpdate}
              className={selectedCustomer?.isActive ? 'bg-red-600 hover:bg-red-700' : 'bg-green-600 hover:bg-green-700'}
            >
              {updateStatusMutation.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin"/>
                  Updating...
                </>
              ) : (
                selectedCustomer?.isActive ? 'Deactivate' : 'Activate'
              )}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Customer Role Update Dialog */}
      <Dialog open={roleDialogOpen} onOpenChange={setRoleDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Change Customer Role</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground">
              Current role for {selectedCustomer?.fullName}: {selectedCustomer?.roleName}
            </p>
            <div className="space-y-2">
              {Object.keys(UserRole)
                .filter(key => isNaN(Number(key)))
                .map((roleName) => (
                  <button
                    key={roleName}
                    onClick={() => handleConfirmRoleUpdate(roleName)}
                    disabled={updateRoleMutation.isPending || selectedCustomer?.roleName === roleName}
                    className={`w-full p-2 text-left border rounded-md hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed ${
                      selectedCustomer?.roleName === roleName ? 'bg-blue-50 border-blue-200' : ''
                    }`}
                  >
                    {roleName}
                  </button>
                ))}
            </div>
            {updateRoleMutation.isPending && (
              <div className="flex items-center justify-center">
                <Loader2 className="h-4 w-4 animate-spin mr-2"/>
                Updating role...
              </div>
            )}
          </div>
        </DialogContent>
      </Dialog>

      {/* Customer Details Dialog */}
      {detailsDialogOpen && (
        <Dialog open={detailsDialogOpen} onOpenChange={setDetailsDialogOpen}>
          <DialogContent className="max-w-2xl">
            <DialogHeader>
              <DialogTitle>
                Customer Details - {selectedCustomer?.fullName}
              </DialogTitle>
            </DialogHeader>
            <div className="space-y-4">
              {selectedCustomer && (
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <h4 className="font-medium">Personal Information</h4>
                    <p className="text-sm text-muted-foreground">
                      Name: {selectedCustomer.fullName}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      Email: {selectedCustomer.email}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      Phone: {selectedCustomer.phone || 'N/A'}
                    </p>
                  </div>
                  <div>
                    <h4 className="font-medium">Account Information</h4>
                    <p className="text-sm text-muted-foreground">
                      Role: {selectedCustomer.roleName}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      Status: {selectedCustomer.isActive ? 'Active' : 'Inactive'}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      Joined: {new Date(selectedCustomer.createdAt).toLocaleDateString()}
                    </p>
                  </div>
                </div>
              )}
            </div>
          </DialogContent>
        </Dialog>
      )}

    </div>
  );
};

export default CustomersPage;
