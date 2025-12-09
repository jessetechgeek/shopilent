import React, {useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {Layers} from 'lucide-react';
import {categoryApi} from '@/api/categories';
import {CategoryTable} from '@/components/catalog/categories/CategoryTable';
import CategoryForm from '@/components/catalog/CategoryForm';
import ParentCategoryModal from '@/components/catalog/ParentCategoryModal';
import {ConfirmDialog} from '@/components/shared/ConfirmDialog';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription
} from '@/components/ui/dialog';
import {
  CategoryDto,
  CreateCategoryRequest,
  UpdateCategoryRequest,
  CategoryDatatableRequest,
  UpdateCategoryStatusRequest,
  CategoryDataTableDto
} from '@/models/catalog';
import {DataTableResult} from '@/models/common';
import {useApiMutation} from '@/hooks/useApiMutation';
import {Button} from "@/components/ui/button.tsx";
import {useTitle} from '@/hooks/useTitle';

const CategoriesPage: React.FC = () => {
  useTitle('Categories');
  // State for table
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const [sortColumn, setSortColumn] = useState(0); // Name column by default
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');

  // State for dialogs
  const [formOpen, setFormOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [detailsDialogOpen, setDetailsDialogOpen] = useState(false);
  const [parentModalOpen, setParentModalOpen] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<CategoryDto | null>(null);
  const [selectedForParentChange, setSelectedForParentChange] = useState<CategoryDto | null>(null);

  // Prepare datatables request
  const datatableRequest: CategoryDatatableRequest = {
    draw: 1,
    start: pageIndex * pageSize,
    length: pageSize,
    search: {
      value: searchQuery,
      regex: false
    },
    columns: [
      {data: 'name', name: 'name', searchable: true, orderable: true, search: {value: '', regex: false}},
      {data: 'slug', name: 'slug', searchable: true, orderable: true, search: {value: '', regex: false}},
      {data: 'parentId', name: 'parentId', searchable: false, orderable: true, search: {value: '', regex: false}},
      {data: 'level', name: 'level', searchable: false, orderable: true, search: {value: '', regex: false}},
      {data: 'isActive', name: 'isActive', searchable: false, orderable: true, search: {value: '', regex: false}}
    ],
    order: [
      {column: sortColumn, dir: sortDirection}
    ]
  };

  // Query categories
  const {data} = useQuery<DataTableResult<CategoryDataTableDto>>({
    queryKey: ['categories', pageIndex, pageSize, searchQuery, sortColumn, sortDirection],
    queryFn: async () => {
      const response = await categoryApi.getCategoriesForDatatable(datatableRequest);
      if (response.data.succeeded) {
        return response.data.data;
      }
      throw new Error(response.data.message || 'Failed to fetch categories');
    }
  });

  // Create category mutation with useApiMutation
  const createCategoryMutation = useApiMutation({
    mutationFn: (categoryData: CreateCategoryRequest) =>
      categoryApi.createCategory(categoryData),
    onSuccessMessage: 'Category created successfully',
    invalidateQueries: ['categories'],
    onSuccess: () => setFormOpen(false)
  });

  // Update category mutation with useApiMutation
  const updateCategoryMutation = useApiMutation({
    mutationFn: ({id, data}: { id: string; data: UpdateCategoryRequest }) =>
      categoryApi.updateCategory(id, data),
    onSuccessMessage: 'Category updated successfully',
    invalidateQueries: ['categories'],
    onSuccess: () => setFormOpen(false)
  });

  // Delete category mutation with useApiMutation
  const deleteCategoryMutation = useApiMutation({
    mutationFn: (id: string) => categoryApi.deleteCategory(id),
    onSuccessMessage: 'Category deleted successfully',
    invalidateQueries: ['categories'],
    onSuccess: () => setDeleteDialogOpen(false)
  });

  // Toggle category status mutation with useApiMutation
  const toggleStatusMutation = useApiMutation({
    mutationFn: ({id, status}: { id: string; status: UpdateCategoryStatusRequest }) =>
      categoryApi.updateCategoryStatus(id, status),
    onSuccessMessage: 'Category status updated successfully',
    invalidateQueries: ['categories']
  });

  // Update parent category mutation with useApiMutation
  const updateParentMutation = useApiMutation({
    mutationFn: ({id, parentId}: { id: string; parentId?: string }) =>
      categoryApi.updateCategoryParent(id, {parentId}),
    onSuccessMessage: 'Category parent updated successfully',
    invalidateQueries: ['categories'],
    onSuccess: () => setParentModalOpen(false)
  });

  // Handle sorting
  const handleSort = (column: number, direction: 'asc' | 'desc') => {
    setSortColumn(column);
    setSortDirection(direction);
  };

  // Handle actions
  const handleAddNew = () => {
    setSelectedCategory(null);
    setFormOpen(true);
  };

  const handleEdit = (category: CategoryDto) => {
    setSelectedCategory(category);
    setFormOpen(true);
  };

  const handleDelete = (category: CategoryDto) => {
    setSelectedCategory(category);
    setDeleteDialogOpen(true);
  };

  const handleViewDetails = (category: CategoryDto) => {
    setSelectedCategory(category);
    setDetailsDialogOpen(true);
  };

  const handleParentClick = (category: CategoryDto) => {
    setSelectedForParentChange(category);
    setParentModalOpen(true);
  };

  const handleToggleStatus = (category: CategoryDto, status: boolean) => {
    toggleStatusMutation.mutate({
      id: category.id,
      status: {isActive: status}
    });
  };

  const handleFormSubmit = async (formData: CreateCategoryRequest | UpdateCategoryRequest) => {
    if (selectedCategory) {
      // Update existing category
      updateCategoryMutation.mutate({
        id: selectedCategory.id,
        data: formData as UpdateCategoryRequest
      });
    } else {
      // Create new category
      createCategoryMutation.mutate(formData as CreateCategoryRequest);
    }
  };

  const handleConfirmDelete = async () => {
    if (selectedCategory) {
      await deleteCategoryMutation.mutateAsync(selectedCategory.id);
    }
  };

  const handleParentUpdate = async (categoryId: string, parentId?: string) => {
    await updateParentMutation.mutateAsync({id: categoryId, parentId});
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Categories</h1>
          <p className="text-muted-foreground">
            Manage your product categories
          </p>
        </div>
        <div className="flex items-center space-x-2">
          <Layers className="h-5 w-5 text-muted-foreground"/>
          <span className="text-sm text-muted-foreground">
                        {data?.recordsTotal || 0} total categories
                    </span>
        </div>
      </div>

      {/* Category Table */}
      <CategoryTable
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
        onParentClick={handleParentClick}
        onAddNew={handleAddNew}
      />

      {/* Category Form Dialog */}
      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>
              {selectedCategory ? 'Edit Category' : 'Create New Category'}
            </DialogTitle>
            <DialogDescription>
              {selectedCategory
                ? 'Update category information'
                : 'Fill in the details to create a new category'}
            </DialogDescription>
          </DialogHeader>
          <CategoryForm
            category={selectedCategory || undefined}
            onSubmit={handleFormSubmit}
            onCancel={() => setFormOpen(false)}
            isLoading={createCategoryMutation.isPending || updateCategoryMutation.isPending}
          />
        </DialogContent>
      </Dialog>

      {/* Category Details Dialog */}
      <Dialog open={detailsDialogOpen} onOpenChange={setDetailsDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Category Details</DialogTitle>
          </DialogHeader>
          {selectedCategory && (
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-4 items-center gap-4">
                <span className="text-sm font-medium">Name:</span>
                <span className="col-span-3">{selectedCategory.name}</span>
              </div>
              <div className="grid grid-cols-4 items-center gap-4">
                <span className="text-sm font-medium">Slug:</span>
                <span className="col-span-3">{selectedCategory.slug}</span>
              </div>
              <div className="grid grid-cols-4 items-center gap-4">
                <span className="text-sm font-medium">Description:</span>
                <span className="col-span-3">{selectedCategory.description || '-'}</span>
              </div>
              <div className="grid grid-cols-4 items-center gap-4">
                <span className="text-sm font-medium">Parent ID:</span>
                <span className="col-span-3">{selectedCategory.parentId || '-'}</span>
              </div>
              <div className="grid grid-cols-4 items-center gap-4">
                <span className="text-sm font-medium">Path:</span>
                <span className="col-span-3">{selectedCategory.path}</span>
              </div>
              <div className="grid grid-cols-4 items-center gap-4">
                <span className="text-sm font-medium">Level:</span>
                <span className="col-span-3">{selectedCategory.level}</span>
              </div>
              <div className="grid grid-cols-4 items-center gap-4">
                <span className="text-sm font-medium">Status:</span>
                <span className="col-span-3">
                  {selectedCategory.isActive ? (
                    <span
                      className="inline-flex items-center rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900/30 dark:text-green-500">
                      Active
                    </span>
                  ) : (
                    <span
                      className="inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-800 dark:bg-gray-900/30 dark:text-gray-400">
                      Inactive
                    </span>
                  )}
                </span>
              </div>
              <div className="grid grid-cols-4 items-center gap-4">
                <span className="text-sm font-medium">Created:</span>
                <span className="col-span-3">
                  {new Date(selectedCategory.createdAt).toLocaleString()}
                </span>
              </div>
              <div className="grid grid-cols-4 items-center gap-4">
                <span className="text-sm font-medium">Last Updated:</span>
                <span className="col-span-3">
                  {new Date(selectedCategory.updatedAt).toLocaleString()}
                </span>
              </div>
            </div>
          )}
          <div className="flex justify-end">
            <Button onClick={() => setDetailsDialogOpen(false)}>Close</Button>
          </div>
        </DialogContent>
      </Dialog>

      {/* Parent Category Modal */}
      <ParentCategoryModal
        open={parentModalOpen}
        onOpenChange={setParentModalOpen}
        category={selectedForParentChange}
        onSubmit={handleParentUpdate}
        isLoading={updateParentMutation.isPending}
      />

      {/* Delete Confirmation Dialog - Using the new ConfirmDialog component */}
      <ConfirmDialog
        title="Delete Category"
        description={`Are you sure you want to delete the category "${selectedCategory?.name}"?
                    This action cannot be undone.`}
        open={deleteDialogOpen}
        onOpenChange={setDeleteDialogOpen}
        onConfirm={handleConfirmDelete}
        isLoading={deleteCategoryMutation.isPending}
        confirmLabel="Delete"
        variant="destructive"
      />
    </div>
  );
};

export default CategoriesPage;
