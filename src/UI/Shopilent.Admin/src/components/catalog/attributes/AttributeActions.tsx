import {Button} from '@/components/ui/button';
import {Tooltip, TooltipContent, TooltipProvider, TooltipTrigger} from '@/components/ui/tooltip';
import {Edit, Trash2, Eye} from 'lucide-react';
import {AttributeDto} from '@/models/catalog';

interface AttributeActionsProps {
  attribute: AttributeDto;
  onEdit: (attribute: AttributeDto) => void;
  onDelete: (attribute: AttributeDto) => void;
  onViewDetails: (attribute: AttributeDto) => void;
}

export function AttributeActions({
                                   attribute,
                                   onEdit,
                                   onDelete,
                                   onViewDetails
                                 }: AttributeActionsProps) {
  return (
    <TooltipProvider>
      <div className="flex items-center justify-end space-x-1">
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              size="icon"
              onClick={() => onViewDetails(attribute)}
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
              onClick={() => onEdit(attribute)}
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
              onClick={() => onDelete(attribute)}
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
