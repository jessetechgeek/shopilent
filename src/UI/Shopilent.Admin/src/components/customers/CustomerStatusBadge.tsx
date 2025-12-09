// src/components/customers/CustomerStatusBadge.tsx
import { Badge } from '@/components/ui/badge';

interface CustomerStatusBadgeProps {
    isActive: boolean;
}

export function CustomerStatusBadge({ isActive }: CustomerStatusBadgeProps) {
    return (
        <Badge
            variant={isActive ? 'success' : 'destructive'}
            className="text-xs"
        >
            {isActive ? 'Active' : 'Inactive'}
        </Badge>
    );
}