// src/components/customers/CustomerEmptyState.tsx
import { Users } from 'lucide-react';

export function CustomerEmptyState() {
    return (
        <div className="flex flex-col items-center justify-center py-12 text-center">
            <Users className="mx-auto h-12 w-12 text-muted-foreground" />
            <h3 className="mt-4 text-lg font-semibold">No customers found</h3>
            <p className="mt-2 text-sm text-muted-foreground">
                No customers match your search criteria.
            </p>
        </div>
    );
}