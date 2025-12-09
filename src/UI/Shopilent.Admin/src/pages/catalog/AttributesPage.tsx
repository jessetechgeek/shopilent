import React, {useState} from 'react';
import {useQuery} from '@tanstack/react-query';
import {List} from 'lucide-react';
import {attributeApi} from '@/api/attributes';
import {AttributeTable} from '@/components/catalog/attributes/AttributeTable';
import AttributeForm from '@/components/catalog/AttributeForm';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription
} from '@/components/ui/dialog';
import {Badge} from '@/components/ui/badge';
import {ConfirmDialog} from '@/components/shared/ConfirmDialog';
import {useApiMutation} from '@/hooks/useApiMutation';
import {
  AttributeDto,
  CreateAttributeRequest,
  UpdateAttributeRequest,
  AttributeDatatableRequest,
  AttributeDataTableDto,
  AttributeType
} from '@/models/catalog';
import {DataTableResult} from '@/models/common';
import {Button} from "@/components/ui/button.tsx";
import {useTitle} from '@/hooks/useTitle';

const AttributesPage: React.FC = () => {
  useTitle('Attributes');
  // Table state
  const [pageIndex, setPageIndex] = useState(0);
  const [pageSize, setPageSize] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const [sortColumn, setSortColumn] = useState(0); // Name column by default
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc');

  // Dialog state
  const [formOpen, setFormOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [detailsDialogOpen, setDetailsDialogOpen] = useState(false);
  const [selectedAttributeId, setSelectedAttributeId] = useState<string | null>(null);
  const [selectedAttribute, setSelectedAttribute] = useState<AttributeDto | null>(null);

  // Prepare datatable request
  const datatableRequest: AttributeDatatableRequest = {
    draw: 1,
    start: pageIndex * pageSize,
    length: pageSize,
    search: {
      value: searchQuery,
      regex: false
    },
    columns: [
      {data: 'name', name: 'name', searchable: true, orderable: true, search: {value: '', regex: false}},
      {data: 'displayName', name: 'displayName', searchable: true, orderable: true, search: {value: '', regex: false}},
      {data: 'type', name: 'type', searchable: false, orderable: true, search: {value: '', regex: false}},
      {data: 'isVariant', name: 'isVariant', searchable: false, orderable: true, search: {value: '', regex: false}},
      {data: 'filterable', name: 'filterable', searchable: false, orderable: true, search: {value: '', regex: false}}
    ],
    order: [
      {column: sortColumn, dir: sortDirection}
    ]
  };

  // Fetch attributes data
  const {data} = useQuery<DataTableResult<AttributeDataTableDto>>({
    queryKey: ['attributes', pageIndex, pageSize, searchQuery, sortColumn, sortDirection],
    queryFn: async () => {
      const response = await attributeApi.getAttributesForDatatable(datatableRequest);
      if (response.data.succeeded) {
        return response.data.data;
      }
      throw new Error(response.data.message || 'Failed to fetch attributes');
    }
  });

  // API Mutations using the new hook
  const createAttributeMutation = useApiMutation({
    mutationFn: (attributeData: CreateAttributeRequest) =>
      attributeApi.createAttribute(attributeData),
    onSuccessMessage: 'Attribute created successfully',
    invalidateQueries: ['attributes'],
    onSuccess: () => {
      setFormOpen(false);
    }
  });

  const updateAttributeMutation = useApiMutation({
    mutationFn: ({id, data}: { id: string; data: UpdateAttributeRequest }) =>
      attributeApi.updateAttribute(id, data),
    onSuccessMessage: 'Attribute updated successfully',
    invalidateQueries: ['attributes'],
    onSuccess: () => {
      setFormOpen(false);
    }
  });

  const deleteAttributeMutation = useApiMutation({
    mutationFn: (id: string) => attributeApi.deleteAttribute(id),
    onSuccessMessage: 'Attribute deleted successfully',
    invalidateQueries: ['attributes'],
    onSuccess: () => {
      setDeleteDialogOpen(false);
    }
  });

  // Handler functions
  const handleSort = (column: number, direction: 'asc' | 'desc') => {
    setSortColumn(column);
    setSortDirection(direction);
  };

  const handleAddNew = () => {
    setSelectedAttributeId(null);
    setSelectedAttribute(null);
    setFormOpen(true);
  };

  const handleEdit = (attribute: AttributeDto) => {
    setSelectedAttributeId(attribute.id);
    setSelectedAttribute(attribute);
    setFormOpen(true);
  };

  const handleDelete = (attribute: AttributeDto) => {
    setSelectedAttribute(attribute);
    setDeleteDialogOpen(true);
  };

  const handleViewDetails = (attribute: AttributeDto) => {
    setSelectedAttribute(attribute);
    setDetailsDialogOpen(true);
  };

  const handleFormSubmit = async (formData: CreateAttributeRequest | UpdateAttributeRequest) => {
    if (selectedAttributeId) {
      // Update existing attribute
      updateAttributeMutation.mutate({
        id: selectedAttributeId,
        data: formData as UpdateAttributeRequest
      });
    } else {
      // Create new attribute
      createAttributeMutation.mutate(formData as CreateAttributeRequest);
    }
  };

  const handleConfirmDelete = async () => {
    if (selectedAttribute) {
      deleteAttributeMutation.mutate(selectedAttribute.id);
    }
  };

  // Format configuration for detail view
  const formatConfigurationDetail = (attribute: AttributeDto): React.ReactNode => {
    if (!attribute || !attribute.configuration) return <span className="text-muted-foreground">No configuration</span>;

    switch (attribute.type) {
      case AttributeType.Text:
        return <span className="text-muted-foreground">No additional configuration</span>;

      case AttributeType.Number:
        return (
          <div>
            <p>Unit: {attribute.configuration.unit || 'Not specified'}</p>
          </div>
        );

      case AttributeType.Boolean:
        return <span className="text-muted-foreground">No additional configuration</span>;

      case AttributeType.Select:
        if (attribute.configuration.values && Array.isArray(attribute.configuration.values)) {
          return (
            <div className="space-y-1">
              <p className="font-medium">Options:</p>
              <div className="flex flex-wrap gap-1">
                {attribute.configuration.values.map((value: string, index: number) => (
                  <Badge key={index} variant="outline">{value}</Badge>
                ))}
              </div>
            </div>
          );
        }
        return <span className="text-muted-foreground">No values defined</span>;

      case AttributeType.Color:
        if (attribute.configuration.values && Array.isArray(attribute.configuration.values)) {
          return (
            <div className="space-y-2">
              <p className="font-medium">Colors:</p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                {attribute.configuration.values.map((color: { name: string; hex: string }, index: number) => (
                  <div key={index} className="flex items-center space-x-2 border p-2 rounded-md">
                    <div
                      className="w-6 h-6 rounded-full border"
                      style={{backgroundColor: color.hex}}
                    />
                    <span>{color.name}</span>
                    <code className="text-xs bg-muted p-1 rounded">{color.hex}</code>
                  </div>
                ))}
              </div>
            </div>
          );
        }
        return <span className="text-muted-foreground">No colors defined</span>;

      case AttributeType.Date:
        return <span className="text-muted-foreground">No additional configuration</span>;

      case AttributeType.Dimensions:
        return (
          <div>
            <p>Unit: {attribute.configuration.unit || 'cm'}</p>
          </div>
        );

      case AttributeType.Weight:
        return (
          <div className="space-y-1">
            <p>Unit: {attribute.configuration.unit || 'kg'}</p>
            <p>Precision: {attribute.configuration.precision !== undefined ? attribute.configuration.precision : 2} decimal
              places</p>
          </div>
        );

      default:
        return <pre className="text-xs p-2 bg-muted rounded overflow-auto">
                    {JSON.stringify(attribute.configuration, null, 2)}
                </pre>;
    }
  };

  // Main render
  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Attributes</h1>
          <p className="text-muted-foreground">
            Manage your product attributes and variants
          </p>
        </div>
        <div className="flex items-center space-x-2">
          <List className="h-5 w-5 text-muted-foreground"/>
          <span className="text-sm text-muted-foreground">
                        {data?.recordsTotal || 0} total attributes
                    </span>
        </div>
      </div>

      {/* Attribute DataTable */}
      <AttributeTable
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
        onViewDetails={handleViewDetails}
        onAddNew={handleAddNew}
      />

      {/* Attribute Form Dialog */}
      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>
              {selectedAttributeId ? 'Edit Attribute' : 'Create New Attribute'}
            </DialogTitle>
            <DialogDescription>
              {selectedAttributeId
                ? 'Update attribute information'
                : 'Fill in the details to create a new attribute'}
            </DialogDescription>
          </DialogHeader>
          <AttributeForm
            attributeId={selectedAttributeId || undefined}
            onSubmit={handleFormSubmit}
            onCancel={() => setFormOpen(false)}
            isLoading={createAttributeMutation.isPending || updateAttributeMutation.isPending}
          />
        </DialogContent>
      </Dialog>

      {/* Attribute Details Dialog */}
      <Dialog open={detailsDialogOpen} onOpenChange={setDetailsDialogOpen}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogHeader>
            <DialogTitle>Attribute Details</DialogTitle>
            <DialogDescription>
              View detailed information about this attribute
            </DialogDescription>
          </DialogHeader>
          {selectedAttribute && (
            <div className="grid gap-4 py-4">
              <div className="grid grid-cols-3 items-center gap-4">
                <span className="text-sm font-medium">System Name:</span>
                <span className="col-span-2 font-mono text-sm">{selectedAttribute.name}</span>
              </div>
              <div className="grid grid-cols-3 items-center gap-4">
                <span className="text-sm font-medium">Display Name:</span>
                <span className="col-span-2">{selectedAttribute.displayName}</span>
              </div>
              <div className="grid grid-cols-3 items-center gap-4">
                <span className="text-sm font-medium">Type:</span>
                <span className="col-span-2">
                                    <Badge variant="secondary">{selectedAttribute.type}</Badge>
                                </span>
              </div>
              <div className="grid grid-cols-3 items-start gap-4">
                <span className="text-sm font-medium">Configuration:</span>
                <div className="col-span-2">
                  {formatConfigurationDetail(selectedAttribute)}
                </div>
              </div>
              <div className="grid grid-cols-3 items-center gap-4">
                <span className="text-sm font-medium">Used for Variants:</span>
                <span className="col-span-2">
                                    {selectedAttribute.isVariant ?
                                      <Badge variant="default" className="bg-green-600">Yes</Badge> :
                                      <Badge variant="outline">No</Badge>
                                    }
                                </span>
              </div>
              <div className="grid grid-cols-3 items-center gap-4">
                <span className="text-sm font-medium">Filterable:</span>
                <span className="col-span-2">
                                    {selectedAttribute.filterable ?
                                      <Badge variant="default" className="bg-green-600">Yes</Badge> :
                                      <Badge variant="outline">No</Badge>
                                    }
                                </span>
              </div>
              <div className="grid grid-cols-3 items-center gap-4">
                <span className="text-sm font-medium">Searchable:</span>
                <span className="col-span-2">
                                    {selectedAttribute.searchable ?
                                      <Badge variant="default" className="bg-green-600">Yes</Badge> :
                                      <Badge variant="outline">No</Badge>
                                    }
                                </span>
              </div>
              <div className="grid grid-cols-3 items-center gap-4">
                <span className="text-sm font-medium">Created:</span>
                <span className="col-span-2 text-sm text-muted-foreground">
                                    {new Date(selectedAttribute.createdAt).toLocaleString()}
                                </span>
              </div>
              <div className="grid grid-cols-3 items-center gap-4">
                <span className="text-sm font-medium">Last Updated:</span>
                <span className="col-span-2 text-sm text-muted-foreground">
                                    {new Date(selectedAttribute.updatedAt).toLocaleString()}
                                </span>
              </div>
            </div>
          )}
          <div className="flex justify-end space-x-2">
            <Button
              variant="outline"
              onClick={() => setDetailsDialogOpen(false)}
            >
              Close
            </Button>
            <Button
              onClick={() => {
                setDetailsDialogOpen(false);
                if (selectedAttribute) {
                  handleEdit(selectedAttribute);
                }
              }}
            >
              Edit
            </Button>
          </div>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation Dialog - Using the new reusable ConfirmDialog component */}
      <ConfirmDialog
        title="Delete Attribute"
        description={`Are you sure you want to delete the attribute "${selectedAttribute?.displayName}"?
                This action cannot be undone and may affect products using this attribute.`}
        open={deleteDialogOpen}
        onOpenChange={setDeleteDialogOpen}
        onConfirm={handleConfirmDelete}
        isLoading={deleteAttributeMutation.isPending}
        confirmLabel="Delete"
        variant="destructive"
      />
    </div>
  );
};

export default AttributesPage;
