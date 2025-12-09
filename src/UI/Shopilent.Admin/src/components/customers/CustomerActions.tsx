import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { Eye, Edit, UserCheck, UserX, Shield } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { UserDatatableDto } from '@/models/customers';

interface CustomerActionsProps {
    customer: UserDatatableDto;
    onViewDetails: (customer: UserDatatableDto) => void;
    onUpdateStatus: (customer: UserDatatableDto) => void;
    onUpdateRole: (customer: UserDatatableDto) => void;
    onEdit: (customer: UserDatatableDto) => void;
}

export function CustomerActions({
    customer,
    onViewDetails,
    onUpdateStatus,
    onUpdateRole,
}: CustomerActionsProps) {
    const navigate = useNavigate();

    // Handle edit navigation
    const handleEdit = () => {
        navigate(`/customers/edit/${customer.id}`);
    };

    return (
        <div className="flex items-center justify-end space-x-1">
            <Tooltip>
                <TooltipTrigger asChild>
                    <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => onViewDetails(customer)}
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
                        onClick={handleEdit}
                    >
                        <Edit className="size-4" />
                        <span className="sr-only">Edit customer</span>
                    </Button>
                </TooltipTrigger>
                <TooltipContent>
                    <p>Edit Customer</p>
                </TooltipContent>
            </Tooltip>

            <Tooltip>
                <TooltipTrigger asChild>
                    <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => onUpdateStatus(customer)}
                        className={customer.isActive 
                            ? "text-red-600 hover:text-red-700 hover:bg-red-50" 
                            : "text-green-600 hover:text-green-700 hover:bg-green-50"
                        }
                    >
                        {customer.isActive ? (
                            <UserX className="size-4" />
                        ) : (
                            <UserCheck className="size-4" />
                        )}
                        <span className="sr-only">
                            {customer.isActive ? 'Deactivate customer' : 'Activate customer'}
                        </span>
                    </Button>
                </TooltipTrigger>
                <TooltipContent>
                    <p>{customer.isActive ? 'Deactivate' : 'Activate'}</p>
                </TooltipContent>
            </Tooltip>

            <Tooltip>
                <TooltipTrigger asChild>
                    <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => onUpdateRole(customer)}
                        className="text-blue-600 hover:text-blue-700 hover:bg-blue-50"
                    >
                        <Shield className="size-4" />
                        <span className="sr-only">Change role</span>
                    </Button>
                </TooltipTrigger>
                <TooltipContent>
                    <p>Change Role</p>
                </TooltipContent>
            </Tooltip>
        </div>
    );
}