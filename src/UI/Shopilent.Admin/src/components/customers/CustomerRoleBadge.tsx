// src/components/customers/CustomerRoleBadge.tsx
import { Badge } from '@/components/ui/badge';
import { UserRole } from '@/models/customers';

interface CustomerRoleBadgeProps {
    role: UserRole;
    roleName: string;
}

export function CustomerRoleBadge({ role, roleName }: CustomerRoleBadgeProps) {
    const getVariant = () => {
        switch (role) {
            case UserRole.Admin:
                return 'destructive';
            case UserRole.Manager:
                return 'warning';
            case UserRole.Customer:
            default:
                return 'secondary';
        }
    };

    return (
        <Badge
            variant={getVariant()}
            className="text-xs"
        >
            {roleName}
        </Badge>
    );
}