// src/components/catalog/NestedCategorySelector.tsx
import React from 'react';
import { CategoryDto } from '@/models/catalog';
import { Badge } from '@/components/ui/badge';
import { AlertCircle, Folder, FolderTree } from 'lucide-react';

interface NestedCategorySelectorProps {
    categories: CategoryDto[];
    selectedCategories: string[];
    onCategoryChange: (categoryId: string) => void;
    error?: string;
}

// Define the enhanced category type with children
type CategoryWithChildren = CategoryDto & { children: CategoryWithChildren[] };

const NestedCategorySelector: React.FC<NestedCategorySelectorProps> = ({
                                                                           categories,
                                                                           selectedCategories,
                                                                           onCategoryChange,
                                                                           error
                                                                       }) => {
    // Function to build hierarchical tree from flat category list
    const buildCategoryTree = (): CategoryWithChildren[] => {
        // Create a map of categories by ID for easy lookup
        const categoriesById = categories.reduce((acc, category) => {
            acc[category.id] = {
                ...category,
                children: []
            };
            return acc;
        }, {} as Record<string, CategoryWithChildren>);

        // Build the tree structure
        const rootCategories: CategoryWithChildren[] = [];

        categories.forEach(category => {
            const categoryWithChildren = categoriesById[category.id];

            if (!category.parentId) {
                // This is a root category
                rootCategories.push(categoryWithChildren);
            } else if (categoriesById[category.parentId]) {
                // Add as child to parent
                categoriesById[category.parentId].children.push(categoryWithChildren);
            }
        });

        // Sort by name
        const sortByName = (a: CategoryDto, b: CategoryDto) => a.name.localeCompare(b.name);

        // Sort root categories
        rootCategories.sort(sortByName);

        // Sort children recursively
        const sortChildren = (items: CategoryWithChildren[]) => {
            items.forEach(item => {
                item.children.sort(sortByName);
                sortChildren(item.children);
            });
        };

        sortChildren(rootCategories);

        return rootCategories;
    };

    // Recursive component to render a category and its children
    const CategoryItem = ({ category, level = 0 }: {
        category: CategoryWithChildren,
        level?: number
    }) => {
        const isSelected = selectedCategories.includes(category.id);
        const paddingLeft = `${level * 1.5}rem`;

        return (
            <>
                <div className={`flex items-center ${level > 0 ? 'mt-1' : 'mt-2'}`} style={{ paddingLeft }}>
                    <input
                        type="checkbox"
                        id={`cat-${category.id}`}
                        checked={isSelected}
                        onChange={() => onCategoryChange(category.id)}
                        className="h-4 w-4 rounded border-gray-300 text-primary focus:ring-primary"
                    />
                    <label
                        htmlFor={`cat-${category.id}`}
                        className="ml-2 text-sm cursor-pointer flex items-center"
                    >
                        {level === 0 ?
                            <Folder className="size-3.5 mr-1.5 text-muted-foreground" /> :
                            <FolderTree className="size-3.5 mr-1.5 text-muted-foreground" />
                        }
                        {category.name}
                        {level === 0 && (
                            <span className="ml-2 text-xs bg-secondary py-0.5 px-1.5 rounded text-secondary-foreground">
                                Root
                            </span>
                        )}
                    </label>
                </div>

                {category.children.length > 0 && (
                    <div className="space-y-1">
                        {category.children.map(child => (
                            <CategoryItem key={child.id} category={child} level={level + 1} />
                        ))}
                    </div>
                )}
            </>
        );
    };

    const categoryTree = buildCategoryTree();

    return (
        <div className="space-y-4">
            <div className="flex flex-wrap gap-2">
                {selectedCategories.length > 0 ? (
                    categories
                        .filter(cat => selectedCategories.includes(cat.id))
                        .map(category => (
                            <Badge key={category.id} className="px-3 py-1">
                                {category.name}
                                <button
                                    type="button"
                                    className="ml-2 text-xs opacity-70 hover:opacity-100"
                                    onClick={() => onCategoryChange(category.id)}
                                >
                                    ×
                                </button>
                            </Badge>
                        ))
                ) : (
                    <p className="text-sm text-muted-foreground">No categories selected</p>
                )}
            </div>

            <div className={`border rounded-md p-3 max-h-[300px] overflow-y-auto ${error ? "border-destructive" : ""}`}>
                {categoryTree.length > 0 ? (
                    <div className="space-y-1">
                        {categoryTree.map(category => (
                            <CategoryItem key={category.id} category={category} />
                        ))}
                    </div>
                ) : (
                    <p className="text-sm text-muted-foreground py-2 text-center">
                        No categories available
                    </p>
                )}
            </div>

            {error && (
                <p className="text-xs text-destructive flex items-center mt-1">
                    <AlertCircle className="size-3 mr-1"/>
                    {error}
                </p>
            )}
        </div>
    );
};

export default NestedCategorySelector;