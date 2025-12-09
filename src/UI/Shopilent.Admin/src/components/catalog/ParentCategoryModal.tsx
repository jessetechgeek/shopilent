// src/components/catalog/ParentCategoryModal.tsx
import React, {useState, useEffect} from 'react';
import {Button} from '@/components/ui/button';
import {Loader2} from 'lucide-react';
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogDescription,
    DialogFooter,
} from '@/components/ui/dialog';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import {CategoryDto} from '@/models/catalog';
import {categoryApi} from '@/api/categories';

interface ParentCategoryModalProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    category: CategoryDto | null;
    onSubmit: (categoryId: string, parentId?: string) => Promise<void>;
    isLoading: boolean;
}

const ParentCategoryModal: React.FC<ParentCategoryModalProps> = ({
                                                                     open,
                                                                     onOpenChange,
                                                                     category,
                                                                     onSubmit,
                                                                     isLoading,
                                                                 }) => {
    const [parentCategories, setParentCategories] = useState<CategoryDto[]>([]);
    const [selectedParentId, setSelectedParentId] = useState<string | undefined>(undefined);
    const [loadingParents, setLoadingParents] = useState(false);

    // Load possible parent categories
    useEffect(() => {
        const loadParentCategories = async () => {
            if (!category) return;

            setLoadingParents(true);
            try {
                const response = await categoryApi.getCategories();
                if (response.data.succeeded) {
                    // Filter out current category and its children
                    let parents = response.data.data;
                    parents = parents.filter(c =>
                        c.id !== category.id &&
                        !c.path.includes(`/${category.id}/`)
                    );
                    setParentCategories(parents);

                    // Set initial selected parent
                    setSelectedParentId(category.parentId);
                }
            } catch (error) {
                console.error('Failed to load parent categories:', error);
            } finally {
                setLoadingParents(false);
            }
        };

        if (open) {
            loadParentCategories();
        }
    }, [category, open]);

    const handleParentChange = (value: string) => {
        setSelectedParentId(value === 'none' ? undefined : value);
    };

    const handleSubmit = async () => {
        if (category) {
            await onSubmit(category.id, selectedParentId);
        }
    };

    return (
        <Dialog open={open} onOpenChange={onOpenChange}>
            <DialogContent className="sm:max-w-[425px]">
                <DialogHeader>
                    <DialogTitle>
                        {category?.parentId
                            ? 'Change Parent Category'
                            : 'Set Parent Category'}
                    </DialogTitle>
                    <DialogDescription>
                        {category?.parentId
                            ? `Select a new parent for the "${category?.name}" category.`
                            : `Set a parent for the "${category?.name}" category or select "None" to keep it as a root category.`}
                    </DialogDescription>
                </DialogHeader>

                <div className="py-4">
                    <div className="space-y-2">
                        <label className="text-sm font-medium">
                            Parent Category
                        </label>
                        <Select
                            value={selectedParentId || 'none'}
                            onValueChange={handleParentChange}
                            disabled={loadingParents || isLoading}
                        >
                            <SelectTrigger>
                                <SelectValue placeholder="Select parent category"/>
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="none">None (Root Category)</SelectItem>
                                {parentCategories.map(parent => (
                                    <SelectItem key={parent.id} value={parent.id}>
                                        {parent.name}
                                    </SelectItem>
                                ))}
                            </SelectContent>
                        </Select>
                    </div>
                </div>

                <DialogFooter>
                    <Button variant="outline" onClick={() => onOpenChange(false)} disabled={isLoading}>
                        Cancel
                    </Button>
                    <Button onClick={handleSubmit} disabled={isLoading || loadingParents}>
                        {isLoading && <Loader2 className="size-4 mr-2 animate-spin"/>}
                        Save Changes
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
};

export default ParentCategoryModal;