// src/pages/catalog/sections/BasicDetailsSection.tsx (corrected)
import React from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';

interface BasicDetailsSectionProps {
    name: string;
    slug: string;
    description: string;
    sku: string;
    errors: Record<string, string>;
    onNameChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
    onInputChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => void;
}

const BasicDetailsSection: React.FC<BasicDetailsSectionProps> = ({
                                                                     name,
                                                                     slug,
                                                                     description,
                                                                     sku,
                                                                     errors,
                                                                     onNameChange,
                                                                     onInputChange
                                                                 }) => {
    return (
        <Card>
            <CardHeader>
                <CardTitle>Basic Details</CardTitle>
                <CardDescription>
                    Enter the essential information about your product.
                </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {/* Product Name */}
                    <FormField
                        label="Product Name"
                        error={errors.name}
                        required
                        htmlFor="name"
                    >
                        <Input
                            id="name"
                            name="name"
                            value={name}
                            onChange={onNameChange}
                            placeholder="e.g., Premium Wireless Headphones"
                            className={errors.name ? "border-destructive" : ""}
                            required
                        />
                    </FormField>

                    {/* Product SKU */}
                    <FormField
                        label="SKU"
                        description="Stock Keeping Unit code (optional)"
                        htmlFor="sku"
                    >
                        <Input
                            id="sku"
                            name="sku"
                            value={sku}
                            onChange={onInputChange}
                            placeholder="e.g., PRD-001-HDP"
                        />
                    </FormField>
                </div>

                {/* Product Slug */}
                <FormField
                    label="Slug"
                    error={errors.slug}
                    description="Used in URLs. Only use letters, numbers, and hyphens."
                    required
                    htmlFor="slug"
                >
                    <Input
                        id="slug"
                        name="slug"
                        value={slug}
                        onChange={onInputChange}
                        placeholder="e.g., premium-wireless-headphones"
                        className={errors.slug ? "border-destructive" : ""}
                        required
                    />
                </FormField>

                {/* Product Description */}
                <FormField
                    label="Description"
                    htmlFor="description"
                >
                    <Textarea
                        id="description"
                        name="description"
                        value={description}
                        onChange={onInputChange}
                        placeholder="Describe your product..."
                        rows={4}
                    />
                </FormField>
            </CardContent>
        </Card>
    );
};

export default BasicDetailsSection;