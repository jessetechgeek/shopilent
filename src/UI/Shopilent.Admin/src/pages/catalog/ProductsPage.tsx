import React, {useState} from 'react';
import {useQuery, useMutation, useQueryClient} from '@tanstack/react-query';
import {useNavigate} from 'react-router-dom';
import {Loader2, ShoppingBag} from 'lucide-react';
import {productApi} from '@/api/products';
import {ProductTable} from '@/components/catalog/products/ProductTable';
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
import {toast} from '@/components/ui/use-toast';
import {
  ProductDto,
  ProductDatatableRequest,
  UpdateProductStatusRequest,
  ProductDataTableDto
} from '@/models/catalog';
import {DataTableResult} from '@/models/common';
import {useTitle} from '@/hooks/useTitle';

const ProductsPage: React.FC = () => {
  useTitle('Products');
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  // State for table
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const [sortColumn, setSortColumn] = useState(1); // Name column by default (index 1 after image column)
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');

  // State for dialogs
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState<ProductDto | null>(null);

  // Prepare datatables request
  const datatableRequest: ProductDatatableRequest = {
    draw: 1,
    start: pageIndex * pageSize,
    length: pageSize,
    search: {
      value: searchQuery,
      regex: false
    },
    columns: [
      {data: 'image', name: 'image', searchable: false, orderable: false, search: {value: '', regex: false}},
      {data: 'name', name: 'name', searchable: true, orderable: true, search: {value: '', regex: false}},
      {data: 'sku', name: 'sku', searchable: true, orderable: true, search: {value: '', regex: false}},
      {data: 'basePrice', name: 'basePrice', searchable: false, orderable: true, search: {value: '', regex: false}},
      {data: 'categories', name: 'categories', searchable: false, orderable: false, search: {value: '', regex: false}},
      {data: 'totalStock', name: 'totalStock', searchable: false, orderable: true, search: {value: '', regex: false}},
      {data: 'isActive', name: 'isActive', searchable: false, orderable: true, search: {value: '', regex: false}}
    ],
    order: [
      {column: sortColumn, dir: sortDirection}
    ]
  };

  // Query products
  const {data} = useQuery<DataTableResult<ProductDataTableDto>>({
    queryKey: ['products', pageIndex, pageSize, searchQuery, sortColumn, sortDirection],
    queryFn: async () => {
      const response = await productApi.getProductsForDatatable(datatableRequest);
      if (response.data.succeeded) {
        return response.data.data;
      }
      throw new Error(response.data.message || 'Failed to fetch products');
    }
  });

  // Toggle product status mutation
  const toggleStatusMutation = useMutation({
    mutationFn: ({id, status}: { id: string; status: UpdateProductStatusRequest }) =>
      productApi.updateProductStatus(id, status),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Product status updated successfully',
        variant: 'success'
      });
      queryClient.invalidateQueries({queryKey: ['products']});
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to update product status',
        variant: 'destructive'
      });
    }
  });

  // Delete product mutation
  const deleteProductMutation = useMutation({
    mutationFn: (id: string) => productApi.deleteProduct(id),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Product deleted successfully',
        variant: 'success'
      });
      queryClient.invalidateQueries({queryKey: ['products']});
      setDeleteDialogOpen(false);
    },
    onError: (error: any) => {
      toast({
        title: 'Error',
        description: error.response?.data?.message || 'Failed to delete product',
        variant: 'destructive'
      });
    }
  });

  // Handle sorting
  const handleSort = (column: number, direction: 'asc' | 'desc') => {
    setSortColumn(column);
    setSortDirection(direction);
  };

  // Handle actions
  const handleAddNew = () => {
    navigate('/catalog/products/new');
  };

  const handleEdit = (product: ProductDto) => {
    navigate(`/catalog/products/edit/${product.id}`);
  };

  const handleDelete = (product: ProductDto) => {
    setSelectedProduct(product);
    setDeleteDialogOpen(true);
  };

  const handleViewDetails = (product: ProductDto) => {
    navigate(`/catalog/products/edit/${product.id}`);
  };

  const handleToggleStatus = (product: ProductDto, status: boolean) => {
    toggleStatusMutation.mutate({
      id: product.id,
      status: {isActive: status}
    });
  };

  const handleConfirmDelete = () => {
    if (selectedProduct) {
      deleteProductMutation.mutate(selectedProduct.id);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Products</h1>
          <p className="text-muted-foreground">
            Manage your products and inventory
          </p>
        </div>
        <div className="flex items-center space-x-2">
          <ShoppingBag className="h-5 w-5 text-muted-foreground"/>
          <span className="text-sm text-muted-foreground">
                        {data?.recordsTotal || 0} total products
                    </span>
        </div>
      </div>

      {/* Product Table */}
      <ProductTable
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
          setPageIndex(0);
        }}
        onSearch={(query) => {
          setSearchQuery(query);
          setPageIndex(0);
        }}
        onSort={handleSort}
        onEdit={handleEdit}
        onDelete={handleDelete}
        onToggleStatus={handleToggleStatus}
        onViewDetails={handleViewDetails}
        onAddNew={handleAddNew}
      />

      {/* Delete Confirmation Dialog */}
      <AlertDialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Product</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete the product "{selectedProduct?.name}"?
              This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {deleteProductMutation.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin"/>
                  Deleting...
                </>
              ) : (
                'Delete'
              )}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
};

export default ProductsPage;
