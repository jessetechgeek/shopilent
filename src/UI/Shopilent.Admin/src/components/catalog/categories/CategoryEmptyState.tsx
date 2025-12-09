import { Button } from '@/components/ui/button';
import { Layers, Plus } from 'lucide-react';

interface CategoryEmptyStateProps {
    onAddNew?: () => void;
}

export function CategoryEmptyState({ onAddNew }: CategoryEmptyStateProps) {
    return (
        <div className="flex flex-col items-center justify-center py-10">
            <Layers className="size-12 text-muted-foreground"/>
            <h3 className="mt-2 text-lg font-medium">No categories found</h3>
            <p className="text-sm text-muted-foreground">
                {onAddNew ? 'Get started by creating a new category.' : 'No results match your search.'}
            </p>
            {onAddNew && (
                <Button onClick={onAddNew} className="mt-4">
                    <Plus className="size-4 mr-2"/>
                    Add Category
                </Button>
            )}
        </div>
    );
}