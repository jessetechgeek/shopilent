import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import { Edit, Trash2, Eye, Check, X } from 'lucide-react';
import { ProductDto } from '@/models/catalog';

interface ProductActionsProps {
    product: ProductDto;
    onEdit: (product: ProductDto) => void;
    onDelete: (product: ProductDto) => void;
    onViewDetails: (product: ProductDto) => void;
    onToggleStatus: (product: ProductDto, status: boolean) => void;
}

export function ProductActions({
                                   product,
                                   onEdit,
                                   onDelete,
                                   onViewDetails,
                                   onToggleStatus
                               }: ProductActionsProps) {
    return (
        <TooltipProvider>
            <div className="flex items-center justify-end space-x-1">
                <Tooltip>
                    <TooltipTrigger asChild>
                        <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => onViewDetails(product)}
                        >
                            <Eye className="size-4" />
                            <span className="sr-only">View details</span>
                        </Button>
                    </TooltipTrigger>
                    <TooltipContent>
                        <p>View Details</p>
                    </TooltipContent>
                </Tooltip>

                <Tooltip>
                    <TooltipTrigger asChild>
                        <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => onEdit(product)}
                        >
                            <Edit className="size-4" />
                            <span className="sr-only">Edit</span>
                        </Button>
                    </TooltipTrigger>
                    <TooltipContent>
                        <p>Edit</p>
                    </TooltipContent>
                </Tooltip>

                <Tooltip>
                    <TooltipTrigger asChild>
                        <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => onToggleStatus(product, !product.isActive)}
                        >
                            {product.isActive ? (
                                <X className="size-4" />
                            ) : (
                                <Check className="size-4" />
                            )}
                            <span className="sr-only">
                                {product.isActive ? 'Deactivate' : 'Activate'}
                            </span>
                        </Button>
                    </TooltipTrigger>
                    <TooltipContent>
                        <p>{product.isActive ? 'Deactivate' : 'Activate'}</p>
                    </TooltipContent>
                </Tooltip>

                <Tooltip>
                    <TooltipTrigger asChild>
                        <Button
                            variant="ghost"
                            size="icon"
                            onClick={() => onDelete(product)}
                            className="text-destructive"
                        >
                            <Trash2 className="size-4" />
                            <span className="sr-only">Delete</span>
                        </Button>
                    </TooltipTrigger>
                    <TooltipContent>
                        <p>Delete</p>
                    </TooltipContent>
                </Tooltip>
            </div>
        </TooltipProvider>
    );
}