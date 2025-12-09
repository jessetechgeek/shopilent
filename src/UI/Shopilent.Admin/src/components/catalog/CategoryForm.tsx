// src/components/catalog/CategoryForm.tsx
import React, { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { Loader2 } from 'lucide-react';
import {
    CategoryDto,
    CreateCategoryRequest,
    UpdateCategoryRequest
} from '@/models/catalog';
import { categoryApi } from '@/api/categories';

interface CategoryFormProps {
    category?: CategoryDto;
    onSubmit: (categoryData: CreateCategoryRequest | UpdateCategoryRequest) => Promise<void>;
    onCancel: () => void;
    isLoading: boolean;
}

const CategoryForm: React.FC<CategoryFormProps> = ({
                                                       category,
                                                       onSubmit,
                                                       onCancel,
                                                       isLoading
                                                   }) => {
    const [parentCategories, setParentCategories] = useState<CategoryDto[]>([]);
    const [loadingParents, setLoadingParents] = useState(false);

    const [formData, setFormData] = useState<CreateCategoryRequest>({
        name: '',
        slug: '',
        description: '',
        parentId: undefined,
        isActive: true
    });

    // Load form data when editing existing category
    useEffect(() => {
        if (category) {
            setFormData({
                name: category.name,
                slug: category.slug,
                description: category.description || '',
                parentId: category.parentId,
                isActive: category.isActive
            });
        }
    }, [category]);

    // Load parent categories for dropdown
    useEffect(() => {
        const loadParentCategories = async () => {
            setLoadingParents(true);
            try {
                const response = await categoryApi.getCategories();
                if (response.data.succeeded) {
                    // Filter out current category and its children if editing
                    let parents = response.data.data;
                    if (category) {
                        parents = parents.filter(c =>
                            c.id !== category.id &&
                            !c.path.includes(`/${category.id}/`)
                        );
                    }
                    setParentCategories(parents);
                }
            } catch (error) {
                console.error('Failed to load parent categories:', error);
            } finally {
                setLoadingParents(false);
            }
        };

        loadParentCategories();
    }, [category]);

    // Handle form input changes
    const handleChange = (
        e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
    ) => {
        const { name, value } = e.target;
        setFormData(prev => ({ ...prev, [name]: value }));
    };

    // Auto-generate slug from name
    const handleNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const name = e.target.value;
        const slug = name
            .toLowerCase()
            .replace(/[^\w\s-]/g, '')
            .replace(/\s+/g, '-');

        setFormData(prev => ({ ...prev, name, slug }));
    };

    // Handle parent category selection
    const handleParentChange = (value: string) => {
        setFormData(prev => ({
            ...prev,
            parentId: value === 'none' ? undefined : value
        }));
    };

    // Handle active status toggle
    const handleActiveChange = (checked: boolean) => {
        setFormData(prev => ({ ...prev, isActive: checked }));
    };

    // Handle form submission
    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        await onSubmit(formData);
    };

    return (
        <form onSubmit={handleSubmit} className="space-y-4">
            {/* Name field */}
            <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="name">
                    Name <span className="text-destructive">*</span>
                </label>
                <Input
                    id="name"
                    name="name"
                    value={formData.name}
                    onChange={handleNameChange}
                    placeholder="Category name"
                    required
                />
            </div>

            {/* Slug field */}
            <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="slug">
                    Slug <span className="text-destructive">*</span>
                </label>
                <Input
                    id="slug"
                    name="slug"
                    value={formData.slug}
                    onChange={handleChange}
                    placeholder="category-slug"
                    required
                />
                <p className="text-xs text-muted-foreground">
                    Used in URLs. Only use letters, numbers, and hyphens.
                </p>
            </div>

            {/* Description field */}
            <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="description">
                    Description
                </label>
                <Textarea
                    id="description"
                    name="description"
                    value={formData.description || ''}
                    onChange={handleChange}
                    placeholder="Category description (optional)"
                    rows={3}
                />
            </div>

            {/* Parent category dropdown */}
            {!category && (
                <div className="space-y-2">
                    <label className="text-sm font-medium">
                        Parent Category
                    </label>
                    <Select
                        value={formData.parentId || 'none'}
                        onValueChange={handleParentChange}
                        disabled={loadingParents}
                    >
                        <SelectTrigger>
                            <SelectValue placeholder="Select parent category" />
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
            )}

            {/* Active status toggle */}
            <div className="flex items-center space-x-2">
                <Switch
                    id="active"
                    checked={formData.isActive}
                    onCheckedChange={handleActiveChange}
                />
                <label htmlFor="active" className="text-sm font-medium cursor-pointer">
                    Active
                </label>
            </div>

            {/* Form actions */}
            <div className="pt-4 flex justify-end space-x-2">
                <Button type="button" variant="outline" onClick={onCancel} disabled={isLoading}>
                    Cancel
                </Button>
                <Button type="submit" disabled={isLoading}>
                    {isLoading && <Loader2 className="size-4 mr-2 animate-spin" />}
                    {category ? 'Update' : 'Create'} Category
                </Button>
            </div>
        </form>
    );
};

export default CategoryForm;