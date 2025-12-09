// src/pages/catalog/sections/CategoriesSection.tsx
import React from 'react';
import { Loader2 } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import NestedCategorySelector from '@/components/catalog/NestedCategorySelector';
import { CategoryDto } from '@/models/catalog';

interface CategoriesSectionProps {
    categories: CategoryDto[] | undefined;
    selectedCategories: string[];
    onCategoryChange: (categoryId: string) => void;
    error?: string;
    isLoading: boolean;
}

const CategoriesSection: React.FC<CategoriesSectionProps> = ({
                                                                 categories,
                                                                 selectedCategories,
                                                                 onCategoryChange,
                                                                 error,
                                                                 isLoading
                                                             }) => {
    return (
        <Card>
            <CardHeader>
                <CardTitle>Product Categories</CardTitle>
                <CardDescription>
                    Assign your product to one or more categories.
                </CardDescription>
            </CardHeader>
            <CardContent>
                {isLoading ? (
                    <div className="flex items-center justify-center py-4">
                        <Loader2 className="size-5 animate-spin text-primary" />
                        <span className="ml-2">Loading categories...</span>
                    </div>
                ) : categories && categories.length > 0 ? (
                    <NestedCategorySelector
                        categories={categories}
                        selectedCategories={selectedCategories}
                        onCategoryChange={onCategoryChange}
                        error={error}
                    />
                ) : (
                    <p className="text-sm text-muted-foreground">No categories available</p>
                )}
            </CardContent>
        </Card>
    );
};

export default CategoriesSection;