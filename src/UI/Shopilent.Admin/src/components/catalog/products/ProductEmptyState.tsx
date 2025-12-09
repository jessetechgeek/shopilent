import { Button } from '@/components/ui/button';
import { ShoppingBag, Plus } from 'lucide-react';

interface ProductEmptyStateProps {
    onAddNew?: () => void;
}

export function ProductEmptyState({ onAddNew }: ProductEmptyStateProps) {
    return (
        <div className="flex flex-col items-center justify-center py-10">
            <ShoppingBag className="size-12 text-muted-foreground"/>
            <h3 className="mt-2 text-lg font-medium">No products found</h3>
            <p className="text-sm text-muted-foreground">
                {onAddNew ? 'Get started by creating a new product.' : 'No results match your search.'}
            </p>
            {onAddNew && (
                <Button onClick={onAddNew} className="mt-4">
                    <Plus className="size-4 mr-2"/>
                    Add Product
                </Button>
            )}
        </div>
    );
}