import {Button} from '@/components/ui/button';
import {Tooltip, TooltipContent, TooltipProvider, TooltipTrigger} from '@/components/ui/tooltip';
import {Edit, Trash2, Eye, Check, X} from 'lucide-react';
import {CategoryDto} from '@/models/catalog';

interface CategoryActionsProps {
  category: CategoryDto;
  onEdit: (category: CategoryDto) => void;
  onDelete: (category: CategoryDto) => void;
  onViewDetails: (category: CategoryDto) => void;
  onToggleStatus: (category: CategoryDto, status: boolean) => void;
}

export function CategoryActions({
                                  category,
                                  onEdit,
                                  onDelete,
                                  onViewDetails,
                                  onToggleStatus
                                }: CategoryActionsProps) {
  return (
    <TooltipProvider>
      <div className="flex items-center justify-end space-x-1">
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => onViewDetails(category)}
            >
              <Eye className="size-4"/>
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
              onClick={() => onEdit(category)}
            >
              <Edit className="size-4"/>
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
              onClick={() => onToggleStatus(category, !category.isActive)}
            >
              {category.isActive ? (
                <X className="size-4"/>
              ) : (
                <Check className="size-4"/>
              )}
              <span className="sr-only">
                                {category.isActive ? 'Deactivate' : 'Activate'}
                            </span>
            </Button>
          </TooltipTrigger>
          <TooltipContent>
            <p>{category.isActive ? 'Deactivate' : 'Activate'}</p>
          </TooltipContent>
        </Tooltip>

        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => onDelete(category)}
              className="text-destructive"
            >
              <Trash2 className="size-4"/>
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
