// src/components/catalog/VariantList.tsx
import React, {useState} from 'react';
import {
    Card,
    CardHeader,
    CardTitle,
    CardDescription,
    CardContent
} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {
    Table,
    TableHeader,
    TableBody,
    TableRow,
    TableHead,
    TableCell
} from '@/components/ui/table';
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogDescription,
    DialogFooter
} from '@/components/ui/dialog';
import {
    AlertDialog,
    AlertDialogContent,
    AlertDialogHeader,
    AlertDialogTitle,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogAction,
    AlertDialogCancel
} from '@/components/ui/alert-dialog';
import {Badge} from '@/components/ui/badge';
import {Input} from '@/components/ui/input';
import {
    Plus,
    Edit,
    Trash2,
    Check,
    X,
    Loader2,
    Tag,
    ArrowUpDown,
    ImageIcon,
    Images
} from 'lucide-react';
import {
    ProductVariantDto,
    AttributeDto,
    CreateProductVariantRequest,
    UpdateProductVariantRequest,
    UpdateVariantStatusRequest,
    UpdateVariantStockRequest
} from '@/models/catalog';
import VariantForm from './VariantForm';
import {Tooltip, TooltipContent, TooltipProvider, TooltipTrigger} from '@/components/ui/tooltip';

interface VariantListProps {
    productId: string;
    variants: ProductVariantDto[];
    attributes: AttributeDto[];
    productAttributes: { attributeId: string; value: any }[];
    currencySymbol: string;
    onAddVariant: (variant: CreateProductVariantRequest) => Promise<void>;
    onUpdateVariant: (id: string, variant: UpdateProductVariantRequest) => Promise<void>;
    onDeleteVariant: (id: string) => Promise<void>;
    onUpdateStatus: (id: string, status: UpdateVariantStatusRequest) => Promise<void>;
    onUpdateStock: (id: string, stock: UpdateVariantStockRequest) => Promise<void>;
    isLoading: boolean;
    basePrice?: number;
    baseSku?: string;
}

const VariantList: React.FC<VariantListProps> = ({
                                                     productId,
                                                     variants = [], // Default to empty array
                                                     attributes = [], // Default to empty array
                                                     productAttributes = [], // Default to empty array
                                                     currencySymbol,
                                                     onAddVariant,
                                                     onUpdateVariant,
                                                     onDeleteVariant,
                                                     onUpdateStatus,
                                                     onUpdateStock,
                                                     isLoading,
                                                     basePrice = 0,
                                                     baseSku = ''
                                                 }) => {
    const [formDialogOpen, setFormDialogOpen] = useState(false);
    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
    const [stockDialogOpen, setStockDialogOpen] = useState(false);
    const [selectedVariant, setSelectedVariant] = useState<ProductVariantDto | null>(null);
    const [stockQuantity, setStockQuantity] = useState<number>(0);

    // Get variant attributes (attributes marked for variants)
    const variantAttributes = attributes.filter(attr => attr.isVariant);

    // Format currency
    const formatCurrency = (amount: number | undefined | null): string => {
        if (amount === undefined || amount === null || isNaN(amount)) {
            return `${currencySymbol}0.00`;
        }
        return `${currencySymbol}${amount.toFixed(2)}`;
    };

    // Format attribute value for display, handling objects correctly
    const formatAttributeValue = (value: any): string => {
        if (value === null || value === undefined) {
            return '';
        }

        if (typeof value === 'object') {
            // Try to find meaningful properties to display
            if ('name' in value) return value.name;
            if ('label' in value) return value.label;
            if ('value' in value) return formatAttributeValue(value.value);

            // For arrays, join the values
            if (Array.isArray(value)) {
                return value.map(formatAttributeValue).join(', ');
            }

            // Fall back to JSON stringify but with better formatting
            try {
                return JSON.stringify(value)
                    .replace(/[{}"\[\]]/g, '')  // Remove JSON syntax chars
                    .replace(/,/g, ', ');       // Add space after commas
            } catch (e) {
                return String(value);
            }
        }

        return String(value);
    };

    // Get attribute display name by ID
    const getAttributeDisplayName = (attributeId: string): string => {
        const attribute = attributes.find(attr => attr.id === attributeId);
        return attribute ? attribute.displayName : 'Unknown Attribute';
    };

    // Handle add new variant
    const handleAddNew = () => {
        setSelectedVariant(null);
        setFormDialogOpen(true);
    };

    // Handle edit variant
    const handleEdit = (variant: ProductVariantDto) => {
        setSelectedVariant(variant);
        setFormDialogOpen(true);
    };

    // Handle delete variant
    const handleDelete = (variant: ProductVariantDto) => {
        setSelectedVariant(variant);
        setDeleteDialogOpen(true);
    };

    // Handle status toggle
    const handleToggleStatus = async (variant: ProductVariantDto) => {
        await onUpdateStatus(variant.id, {isActive: !variant.isActive});
    };

    // Handle stock dialog open
    const handleStockEdit = (variant: ProductVariantDto) => {
        setSelectedVariant(variant);
        setStockQuantity(variant.stockQuantity);
        setStockDialogOpen(true);
    };

    // Handle form submit
    const handleFormSubmit = async (variantData: CreateProductVariantRequest | UpdateProductVariantRequest) => {
        if (selectedVariant) {
            await onUpdateVariant(selectedVariant.id, variantData as UpdateProductVariantRequest);
        } else {
            await onAddVariant(variantData as CreateProductVariantRequest);
        }
        setFormDialogOpen(false);
        setSelectedVariant(null);
    };

    // Handle delete confirm
    const handleDeleteConfirm = async () => {
        if (selectedVariant) {
            await onDeleteVariant(selectedVariant.id);
            setDeleteDialogOpen(false);
            setSelectedVariant(null);
        }
    };

    // Handle stock update - FIXED: Use 'quantity' property instead of 'stockQuantity'
    const handleStockUpdate = async () => {
        if (selectedVariant) {
            await onUpdateStock(selectedVariant.id, {quantity: stockQuantity});
            setStockDialogOpen(false);
            setSelectedVariant(null);
        }
    };

    // Get variant default image URL
    const getVariantDefaultImage = (variant: ProductVariantDto): string | null => {
        if (variant.images && variant.images.length > 0) {
            // Find default image or use first image
            const defaultImage = variant.images.find(img => img.isDefault) || variant.images[0];

            // Use imageUrl from API response (already includes presigned/public URL)
            if (defaultImage.imageUrl) {
                return defaultImage.imageUrl;
            } else if (defaultImage.url) {
                // Fallback to url field (for blob URLs during upload)
                return defaultImage.url;
            }
        }
        return null;
    };

    // Get image count for variant
    const getVariantImageCount = (variant: ProductVariantDto): number => {
        return variant.images ? variant.images.length : 0;
    };

    return (
        <Card>
            <CardHeader>
                <CardTitle>Product Variants</CardTitle>
                <CardDescription>
                    Manage different variations of your product with unique attributes, pricing, and images.
                </CardDescription>
            </CardHeader>
            <CardContent>
                {variants.length === 0 ? (
                    <div className="text-center py-8">
                        <Tag className="size-12 mx-auto text-muted-foreground mb-4"/>
                        <h3 className="text-lg font-medium mb-2">No variants yet</h3>
                        <p className="text-muted-foreground mb-4">
                            Create variants to offer different options for your product.
                        </p>
                        <Button onClick={handleAddNew}>
                            <Plus className="size-4 mr-2"/>
                            Add First Variant
                        </Button>
                    </div>
                ) : (
                    <div className="space-y-4">
                        <div className="flex justify-between items-center">
                            <p className="text-sm text-muted-foreground">
                                {variants.length} variant{variants.length !== 1 ? 's' : ''} found
                            </p>
                            <Button onClick={handleAddNew} type="button">
                                <Plus className="size-4 mr-2"/>
                                Add Variant
                            </Button>
                        </div>

                        <div className="border rounded-lg overflow-hidden">
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead className="w-16">Image</TableHead>
                                        <TableHead>SKU</TableHead>
                                        <TableHead>Attributes</TableHead>
                                        <TableHead>Price</TableHead>
                                        <TableHead>Stock</TableHead>
                                        <TableHead>Status</TableHead>
                                        <TableHead>Images</TableHead>
                                        <TableHead className="text-right">Actions</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {variants.map((variant) => {
                                        const defaultImage = getVariantDefaultImage(variant);
                                        const imageCount = getVariantImageCount(variant);

                                        return (
                                            <TableRow key={variant.id}>
                                                <TableCell>
                                                    <div
                                                        className="size-10 rounded border overflow-hidden bg-muted flex items-center justify-center">
                                                        {defaultImage ? (
                                                            <img
                                                                src={defaultImage}
                                                                alt={`Variant ${variant.sku}`}
                                                                className="size-full object-cover"
                                                            />
                                                        ) : (
                                                            <ImageIcon className="size-4 text-muted-foreground"/>
                                                        )}
                                                    </div>
                                                </TableCell>
                                                <TableCell>
                                                    {variant.sku ? (
                                                        <Badge variant="outline" className="font-mono">
                                                            <Tag className="size-3 mr-1"/>
                                                            {variant.sku}
                                                        </Badge>
                                                    ) : (
                                                        <span className="text-xs text-muted-foreground">No SKU</span>
                                                    )}
                                                </TableCell>
                                                <TableCell>
                                                    <div className="flex flex-wrap gap-1">
                                                        {/* FIXED: Add safety check for variant.attributes */}
                                                        {variant.attributes && variant.attributes.length > 0 ? (
                                                            variant.attributes.map((attr) => (
                                                                <TooltipProvider key={attr.attributeId}>
                                                                    <Tooltip>
                                                                        <TooltipTrigger asChild
                                                                                        onClick={(e) => {
                                                                                            e.preventDefault();
                                                                                            e.stopPropagation();
                                                                                        }}>
                                                                            <Badge variant="secondary"
                                                                                   className="text-xs">
                                                                                {formatAttributeValue(attr.value)}
                                                                            </Badge>
                                                                        </TooltipTrigger>
                                                                        <TooltipContent>
                                                                            <p>{getAttributeDisplayName(attr.attributeId)}: {formatAttributeValue(attr.value)}</p>
                                                                        </TooltipContent>
                                                                    </Tooltip>
                                                                </TooltipProvider>
                                                            ))
                                                        ) : (
                                                            <span className="text-xs text-muted-foreground">No attributes</span>
                                                        )}
                                                    </div>
                                                </TableCell>
                                                <TableCell className="font-medium">
                                                    {formatCurrency(variant.price)}
                                                </TableCell>
                                                <TableCell>
                                                    <Button
                                                        type="button"
                                                        variant="ghost"
                                                        size="sm"
                                                        onClick={() => handleStockEdit(variant)}
                                                        className="p-0 h-auto font-normal"
                                                    >
                                                        <span
                                                            className={variant.stockQuantity > 0 ? "text-green-600" : "text-red-500"}>
                                                            {variant.stockQuantity > 0 ? variant.stockQuantity : "Out of stock"}
                                                        </span>
                                                        <ArrowUpDown className="size-3 ml-1"/>
                                                    </Button>
                                                </TableCell>
                                                <TableCell>
                                                    <Button
                                                        type="button"
                                                        variant="ghost"
                                                        size="sm"
                                                        onClick={() => handleToggleStatus(variant)}
                                                        className="p-0 h-auto"
                                                    >
                                                        {variant.isActive ? (
                                                            <Badge variant="default"
                                                                   className="bg-green-100 text-green-800 hover:bg-green-200">
                                                                <Check className="size-3 mr-1"/>
                                                                Active
                                                            </Badge>
                                                        ) : (
                                                            <Badge variant="secondary"
                                                                   className="bg-gray-100 text-gray-800 hover:bg-gray-200">
                                                                <X className="size-3 mr-1"/>
                                                                Inactive
                                                            </Badge>
                                                        )}
                                                    </Button>
                                                </TableCell>
                                                <TableCell>
                                                    <div className="flex items-center gap-1">
                                                        <Images className="size-4 text-muted-foreground"/>
                                                        <span className="text-sm text-muted-foreground">
                                                            {imageCount}
                                                        </span>
                                                    </div>
                                                </TableCell>
                                                <TableCell className="text-right">
                                                    <div className="flex justify-end gap-1">
                                                        <Button
                                                            type="button"
                                                            variant="ghost"
                                                            size="sm"
                                                            onClick={() => handleEdit(variant)}
                                                        >
                                                            <Edit className="size-4"/>
                                                        </Button>
                                                        <Button
                                                            type="button"
                                                            variant="ghost"
                                                            size="sm"
                                                            onClick={() => handleDelete(variant)}
                                                            className="text-red-600 hover:text-red-800"
                                                        >
                                                            <Trash2 className="size-4"/>
                                                        </Button>
                                                    </div>
                                                </TableCell>
                                            </TableRow>
                                        );
                                    })}
                                </TableBody>
                            </Table>
                        </div>
                    </div>
                )}

                {/* Variant Form Dialog */}
                <Dialog open={formDialogOpen} onOpenChange={setFormDialogOpen}>
                    <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
                        <DialogHeader>
                            <DialogTitle>
                                {selectedVariant ? 'Edit Variant' : 'Add New Variant'}
                            </DialogTitle>
                            <DialogDescription>
                                {selectedVariant
                                    ? 'Update the variant details below.'
                                    : 'Create a new variant with unique attributes and pricing.'
                                }
                            </DialogDescription>
                        </DialogHeader>
                        <VariantForm
                            variant={selectedVariant || undefined}
                            productId={productId}
                            attributes={attributes}
                            productAttributes={productAttributes}
                            variantAttributes={variantAttributes}
                            existingVariants={variants}
                            onSubmit={handleFormSubmit}
                            onCancel={() => setFormDialogOpen(false)}
                            isLoading={isLoading}
                            basePrice={basePrice}
                            baseSku={baseSku}
                        />
                    </DialogContent>
                </Dialog>

                {/* Delete Confirmation Dialog */}
                <AlertDialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
                    <AlertDialogContent>
                        <AlertDialogHeader>
                            <AlertDialogTitle>Delete Variant</AlertDialogTitle>
                            <AlertDialogDescription>
                                Are you sure you want to delete this variant? This action cannot be undone.
                                {selectedVariant && (
                                    <div className="mt-2 p-2 bg-muted rounded text-sm">
                                        <strong>SKU:</strong> {selectedVariant.sku || 'No SKU'}<br/>
                                        <strong>Price:</strong> {formatCurrency(selectedVariant.price)}
                                    </div>
                                )}
                            </AlertDialogDescription>
                        </AlertDialogHeader>
                        <AlertDialogFooter>
                            <AlertDialogCancel>Cancel</AlertDialogCancel>
                            <AlertDialogAction
                                onClick={handleDeleteConfirm}
                                className="bg-red-600 hover:bg-red-700"
                            >
                                {isLoading ? (
                                    <>
                                        <Loader2 className="size-4 mr-2 animate-spin"/>
                                        Deleting...
                                    </>
                                ) : (
                                    'Delete Variant'
                                )}
                            </AlertDialogAction>
                        </AlertDialogFooter>
                    </AlertDialogContent>
                </AlertDialog>

                {/* Stock Update Dialog */}
                <Dialog open={stockDialogOpen} onOpenChange={setStockDialogOpen}>
                    <DialogContent className="sm:max-w-md">
                        <DialogHeader>
                            <DialogTitle>Update Stock</DialogTitle>
                            <DialogDescription>
                                Update the stock quantity for this variant.
                                {selectedVariant && (
                                    <div className="mt-2 text-sm">
                                        <strong>SKU:</strong> {selectedVariant.sku || 'No SKU'}
                                    </div>
                                )}
                            </DialogDescription>
                        </DialogHeader>
                        <div className="grid gap-4 py-4">
                            <div className="space-y-2">
                                <label htmlFor="stock" className="text-sm font-medium">
                                    Stock Quantity
                                </label>
                                <Input
                                    id="stock"
                                    type="number"
                                    min="0"
                                    value={stockQuantity}
                                    onChange={(e) => setStockQuantity(parseInt(e.target.value) || 0)}
                                    placeholder="Enter stock quantity"
                                />
                            </div>
                        </div>
                        <DialogFooter>
                            <Button variant="outline" onClick={() => setStockDialogOpen(false)}>
                                Cancel
                            </Button>
                            <Button onClick={handleStockUpdate} disabled={isLoading}>
                                {isLoading ? (
                                    <>
                                        <Loader2 className="size-4 mr-2 animate-spin"/>
                                        Updating...
                                    </>
                                ) : (
                                    'Update Stock'
                                )}
                            </Button>
                        </DialogFooter>
                    </DialogContent>
                </Dialog>
            </CardContent>
        </Card>
    );
};

export default VariantList;