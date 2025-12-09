// src/components/catalog/VariantForm.tsx
import React, {useState, useEffect, useMemo, useCallback} from 'react';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {Switch} from '@/components/ui/switch';
import {Loader2, AlertCircle, Tag, ImageIcon} from 'lucide-react';
import {Badge} from '@/components/ui/badge';
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card';
import {
    ProductVariantDto,
    CreateProductVariantRequest,
    UpdateProductVariantRequest,
    AttributeDto,
    AttributeType
} from '@/models/catalog';
import VariantImageUpload from './VariantImageUpload';

interface VariantImageData {
    url: string;
    file?: File;
    imageKey?: string;
    altText?: string;
    isDefault?: boolean;
    displayOrder?: number;
}

interface VariantFormProps {
    variant?: ProductVariantDto;
    productId: string;
    attributes: AttributeDto[];
    productAttributes: { attributeId: string; value: any }[];
    variantAttributes: AttributeDto[];
    existingVariants: ProductVariantDto[]; // Added to track existing variants
    onSubmit: (variantData: CreateProductVariantRequest | UpdateProductVariantRequest) => Promise<void>;
    onCancel: () => void;
    isLoading: boolean;
    basePrice?: number; // Add base price from product
    baseSku?: string; // Add base SKU from product
}

interface VariantCombination {
    id: string;
    label: string;
    attributes: { attributeId: string; value: any }[];
}

const VariantForm: React.FC<VariantFormProps> = ({
                                                     variant,
                                                     productId,
                                                     attributes,
                                                     productAttributes,
                                                     variantAttributes,
                                                     existingVariants,
                                                     onSubmit,
                                                     onCancel,
                                                     isLoading,
                                                     basePrice = 0,
                                                     baseSku = ''
                                                 }) => {
    // Default form data
    const [formData, setFormData] = useState<CreateProductVariantRequest | UpdateProductVariantRequest>({
        productId: productId,
        sku: '',
        price: basePrice, // Use base price from product as default
        stockQuantity: 0,
        isActive: true,
        attributes: [],
        metadata: {},
        images: [] // Add images support
    });

    // State for validation errors
    const [attributeErrors, setAttributeErrors] = useState<Record<string, string>>({});

    // New state for available combinations
    const [availableCombinations, setAvailableCombinations] = useState<VariantCombination[]>([]);
    const [selectedCombination, setSelectedCombination] = useState<string | null>(null);

    // State for variant images
    const [variantImages, setVariantImages] = useState<VariantImageData[]>([]);
    const [removedImageKeys, setRemovedImageKeys] = useState<string[]>([]);

    // Format attribute value for display, handling objects correctly
    const formatAttributeValue = (value: any): string => {
        if (value === null || value === undefined) {
            return '';
        }

        if (typeof value === 'object') {
            // Try to find meaningful properties to display
            if ('name' in value) return value.name;
            if ('label' in value) return value.label;
            if ('value' in value) return formatAttributeValue(value.value);
        }

        return String(value);
    };

    // Get variant attributes that are available for creating combinations
    const availableVariantAttributes = useMemo(() => {
        return variantAttributes.filter(attr => attr.isVariant);
    }, [variantAttributes]);

    // Map product attributes correctly and normalize values
    const normalizedProductAttributes = useMemo(() => {
        return productAttributes.map(attr => {
            // Handle both direct value and nested value structure
            const value = (attr.value && typeof attr.value === 'object' && 'value' in attr.value)
                ? attr.value.value
                : attr.value;

            return {
                attributeId: attr.attributeId,
                value: value
            };
        });
    }, [productAttributes]);

    // Generate all possible combinations of variant attributes
    const generateAvailableCombinations = useCallback(() => {
        // If no variant attributes available, return empty array
        if (!availableVariantAttributes.length) return [];

        // Get attribute ID to name mapping for generating labels
        const attributeNameMap = new Map<string, string>();
        attributes.forEach(attr => {
            attributeNameMap.set(attr.id, attr.displayName);
        });

        // First, collect all existing combinations as keys for fast lookup
        const existingCombinationKeys = new Set<string>();

        existingVariants.forEach(existingVariant => {
            // Skip the current variant being edited
            if (variant && existingVariant.id === variant.id) return;

            if (existingVariant.attributes && existingVariant.attributes.length > 0) {
                // Create a key using sorted attribute values
                const key = existingVariant.attributes
                    .map(attr => `${attr.attributeId}:${JSON.stringify(attr.value.value)}`)
                    .sort()
                    .join('|');

                existingCombinationKeys.add(key);
            }
        });

        // Function to recursively generate all combinations
        const generateCombinations = (
            index: number,
            currentCombination: { attributeId: string; value: any }[]
        ): VariantCombination[] => {
            // Base case: if we've processed all attributes, return the current combination
            if (index >= availableVariantAttributes.length) {
                // Sort attributes by ID for consistent ordering
                const sortedAttrs = [...currentCombination].sort((a, b) =>
                    a.attributeId.localeCompare(b.attributeId)
                );

                // Create a key for this combination
                const combinationKey = sortedAttrs
                    .map(attr => `${attr.attributeId}:${JSON.stringify(attr.value)}`)
                    .join('|');

                // Check if this combination already exists
                if (existingCombinationKeys.has(combinationKey)) {
                    return []; // Skip this combination
                }

                // Generate a readable label
                const label = sortedAttrs.map(attr => {
                    const attrName = attributeNameMap.get(attr.attributeId) || 'Unknown';
                    const valueStr = formatAttributeValue(attr.value);
                    return `${attrName}: ${valueStr}`;
                }).join(' | ');

                return [{
                    id: combinationKey,
                    label,
                    attributes: sortedAttrs
                }];
            }

            // Get the current attribute we're processing
            const currentAttr = availableVariantAttributes[index];

            // Find this attribute's values from product attributes
            const attributeValue = normalizedProductAttributes.find(
                pa => pa.attributeId === currentAttr.id
            );

            if (!attributeValue || !attributeValue.value) {
                // If this attribute has no value, skip to next
                return generateCombinations(index + 1, currentCombination);
            }

            let result: VariantCombination[] = [];

            // Handle different attribute types
            let possibleValues: any[] = [];

            // FIXED: Changed AttributeType.SELECT to AttributeType.Select (PascalCase)
            if (currentAttr.type === AttributeType.Select && Array.isArray(attributeValue.value)) {
                possibleValues = attributeValue.value;
            } else if (Array.isArray(attributeValue.value)) {
                possibleValues = attributeValue.value;
            } else {
                possibleValues = [attributeValue.value];
            }

            // Generate combinations for each possible value
            possibleValues.forEach(value => {
                const newCombination = [
                    ...currentCombination,
                    {attributeId: currentAttr.id, value}
                ];

                const nextCombinations = generateCombinations(index + 1, newCombination);
                result = [...result, ...nextCombinations];
            });

            return result;
        };

        // Start the recursive combination generation
        return generateCombinations(0, []);
    }, [availableVariantAttributes, attributes, normalizedProductAttributes, existingVariants, variant]);

    // Calculate available combinations when dependencies change
    useEffect(() => {
        // Reset selections and combinations to ensure we're starting fresh
        setSelectedCombination(null);

        // Generate new combinations
        const combinations = generateAvailableCombinations();
        console.log(`Generated ${combinations.length} available combinations`);

        setAvailableCombinations(combinations);
    }, [generateAvailableCombinations, existingVariants]);

    // Load existing variant data when editing or create new defaults
    useEffect(() => {
        if (variant) {
            // When editing an existing variant
            setFormData({
                sku: variant.sku || '',
                price: variant.price,
                stockQuantity: variant.stockQuantity,
                isActive: variant.isActive,
                attributes: variant.attributes.map(attr => ({
                    attributeId: attr.attributeId,
                    value: attr.value
                })),
                metadata: variant.metadata,
                images: [] // Will be populated from variant.images
            });

            // Load existing images if available
            if (variant.images && variant.images.length > 0) {
                const existingImages: VariantImageData[] = variant.images.map((img, index) => ({
                    url: img.url,
                    imageKey: img.imageKey,
                    altText: img.altText || `Variant image ${index + 1}`,
                    isDefault: img.isDefault,
                    displayOrder: img.displayOrder
                }));
                setVariantImages(existingImages);
            }
        } else {
            // For new variants, initialize with empty attributes and the base price
            const initialAttributes = availableVariantAttributes.map(attr => ({
                attributeId: attr.id,
                value: undefined
            }));

            setFormData(prev => ({
                ...prev,
                productId,
                sku: '', // We'll generate this from baseSku when attributes are selected
                price: basePrice, // Use the product's base price
                stockQuantity: 0,
                isActive: true,
                attributes: initialAttributes,
                metadata: {},
                images: []
            }));
        }
    }, [variant, productId, availableVariantAttributes, basePrice]);

    // Check if the current variant combination already exists
    const checkForDuplicates = (): boolean => {
        if (!formData.attributes || formData.attributes.length === 0) return false;

        // Create a key for the current combination
        const currentCombination = formData.attributes
            .map(attr => `${attr.attributeId}:${JSON.stringify(attr.value)}`)
            .sort()
            .join('|');

        // Check against existing variants
        for (const existingVariant of existingVariants) {
            // Skip the current variant being edited
            if (variant && existingVariant.id === variant.id) continue;

            if (existingVariant.attributes && existingVariant.attributes.length > 0) {
                // Create a key for this existing variant
                const existingKey = existingVariant.attributes
                    .map(attr => `${attr.attributeId}:${JSON.stringify(attr.value)}`)
                    .sort()
                    .join('|');

                if (existingKey === currentCombination) {
                    return true;
                }
            }
        }

        return false;
    };

    // Generate a suggested SKU based on variant attributes and base SKU
    const generateSuggestedSku = (attrs = formData.attributes): string => {
        if (!attrs || attrs.length === 0) return baseSku;

        // Create SKU suffix from attribute values
        const attributeValues = attrs.map(attr => {
            const attribute = getAttributeById(attr.attributeId);
            if (!attribute || !attr.value) return '';

            // Format the value based on attribute type
            let formattedValue = '';
            if (typeof attr.value === 'string') {
                formattedValue = attr.value.toUpperCase().substring(0, 3).replace(/\s+/g, '');
            } else if (typeof attr.value === 'number') {
                formattedValue = attr.value.toString();
            } else if (typeof attr.value === 'boolean') {
                formattedValue = attr.value ? 'Y' : 'N';
            } else if (typeof attr.value === 'object') {
                formattedValue = formatAttributeValue(attr.value).substring(0, 3).toUpperCase();
            }

            return formattedValue;
        }).filter(Boolean).join('-');

        // Combine base SKU with attribute values
        if (baseSku && attributeValues) {
            return `${baseSku}-${attributeValues}`;
        } else if (baseSku) {
            return baseSku;
        } else if (attributeValues) {
            return `${productId.substring(0, 4)}-${attributeValues}`;
        } else {
            return `${productId.substring(0, 4)}`;
        }
    };

    // Handle combination selection
    const handleCombinationSelect = (combinationId: string) => {
        setSelectedCombination(combinationId);
        setAttributeErrors({});

        // Find the selected combination
        const combination = availableCombinations.find(c => c.id === combinationId);
        if (!combination) return;

        // Update form data with the selected combination's attributes
        setFormData(prev => ({
            ...prev,
            attributes: combination.attributes
        }));

        // Generate and set SKU based on the selected combination
        const generatedSku = generateSuggestedSku(combination.attributes);
        setFormData(prev => ({
            ...prev,
            attributes: combination.attributes,
            sku: generatedSku
        }));
    };

    // Handle input changes
    const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const {name, value} = e.target;
        setFormData(prev => ({...prev, [name]: value}));
    };

    // Handle number input changes
    const handleNumberChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const {name, value} = e.target;
        setFormData(prev => ({...prev, [name]: parseFloat(value)}));
    };

    // Handle switch toggle
    const handleSwitchChange = (name: string, checked: boolean) => {
        setFormData(prev => ({...prev, [name]: checked}));
    };

    // Get attribute by ID
    const getAttributeById = useMemo(() => {
        return (attributeId: string): AttributeDto | undefined => {
            return attributes.find(attr => attr.id === attributeId);
        };
    }, [attributes]);

    // Generate a suggested SKU button
    const handleGenerateSku = () => {
        const suggestedSku = generateSuggestedSku();
        if (suggestedSku) {
            setFormData(prev => ({...prev, sku: suggestedSku}));
        }
    };

    // Handle image changes
    const handleImagesChange = (images: VariantImageData[]) => {
        setVariantImages(images);

        // Update form data with image information
        setFormData(prev => ({
            ...prev,
            images: images.map(img => ({
                file: img.file,
                altText: img.altText,
                isDefault: img.isDefault,
                displayOrder: img.displayOrder,
                imageKey: img.imageKey
            }))
        }));
    };

    // Handle image removal
    const handleImageRemove = (imageKey: string) => {
        if (imageKey) {
            setRemovedImageKeys(prev => [...prev, imageKey]);
        }
    };

    // Validate the form before submission
    const validateForm = (): boolean => {
        let isValid = true;
        const errors: Record<string, string> = {};

        // Check for duplicate variant combinations
        if (checkForDuplicates()) {
            errors.duplicate = "This variant combination already exists";
            isValid = false;
        }

        // Check that all variant attributes have values
        formData.attributes?.forEach(attr => {
            const attribute = getAttributeById(attr.attributeId);
            if (!attribute) return;

            // Check if the value exists and is appropriate for the attribute type
            let isValueValid = true;
            if (attr.value === undefined || attr.value === null || attr.value === '') {
                isValueValid = false;
            } else if (Array.isArray(attr.value) && attr.value.length === 0) {
                isValueValid = false;
            }

            if (!isValueValid) {
                errors[attr.attributeId] = `${attribute.displayName} is required`;
                isValid = false;
            }
        });

        setAttributeErrors(errors);
        return isValid;
    };

    // Handle form submission
    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        e.stopPropagation(); // Stop event propagation to prevent parent form submission

        // Validate form
        if (!validateForm()) {
            return;
        }

        // Generate SKU if empty
        const finalFormData = {...formData};
        if (!finalFormData.sku) {
            finalFormData.sku = generateSuggestedSku();
        }

        // Add image files and metadata
        if (variantImages.length > 0) {
            finalFormData.file = variantImages
                .filter(img => img.file)
                .map(img => img.file!);

            finalFormData.images = variantImages.map(img => ({
                file: img.file,
                altText: img.altText,
                isDefault: img.isDefault,
                displayOrder: img.displayOrder,
                imageKey: img.imageKey
            }));
        }

        // Add removed images for cleanup
        if (variant && removedImageKeys.length > 0) {
            (finalFormData as UpdateProductVariantRequest).imagesToRemove = removedImageKeys;
        }

        // For variant updates, ALWAYS include imageOrders (similar to product updates)
        if (variant && variantImages.length > 0) {
            (finalFormData as UpdateProductVariantRequest).imageOrders = variantImages
                .filter(img => img.imageKey && !removedImageKeys.includes(img.imageKey))
                .map((img, index) => ({
                    imageKey: img.imageKey!,
                    displayOrder: index,
                    isDefault: index === 0
                }));

            console.log('🚀 Variant update request with imageOrders:', finalFormData);
        }

        // If creating new variant, add productId
        const formDataToSubmit = variant
            ? finalFormData as UpdateProductVariantRequest
            : {...finalFormData, productId} as CreateProductVariantRequest;

        await onSubmit(formDataToSubmit);
    };

    return (
        <form onSubmit={handleSubmit} className="space-y-6">
            {/* Variant Combinations Section */}
            {!variant && (
                <div className="space-y-2">
                    <label className="text-sm font-medium">
                        Select Variant Combination
                    </label>

                    {attributeErrors.duplicate && (
                        <div
                            className="bg-destructive/10 text-destructive p-3 rounded-md text-sm flex items-center mb-2">
                            <AlertCircle className="size-4 mr-2"/>
                            {attributeErrors.duplicate}
                        </div>
                    )}

                    {availableCombinations.length === 0 ? (
                        <div className="p-4 border border-dashed rounded-md text-center">
                            <p className="text-sm text-muted-foreground">
                                No available combinations. All possible combinations have been created.
                            </p>
                        </div>
                    ) : (
                        <div className="flex flex-wrap gap-2 p-4 border rounded-md bg-muted/30">
                            {availableCombinations.map(combo => (
                                <Badge
                                    key={combo.id}
                                    variant={selectedCombination === combo.id ? "default" : "outline"}
                                    className={`
                                        cursor-pointer px-3 py-1.5 text-xs 
                                        ${selectedCombination === combo.id ? "bg-primary" : "hover:bg-secondary/50"}
                                    `}
                                    onClick={() => handleCombinationSelect(combo.id)}
                                >
                                    <Tag className="size-3 mr-1.5"/>
                                    {combo.label}
                                </Badge>
                            ))}
                        </div>
                    )}

                    <p className="text-xs text-muted-foreground">
                        Select a combination of attributes for this variant.
                    </p>
                </div>
            )}

            {/* Basic Information */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                    <label className="text-sm font-medium">SKU</label>
                    <div className="flex gap-2">
                        <Input
                            name="sku"
                            value={formData.sku}
                            onChange={handleInputChange}
                            placeholder="Enter SKU"
                        />
                        <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            onClick={handleGenerateSku}
                            disabled={!formData.attributes || formData.attributes.length === 0}
                        >
                            Generate
                        </Button>
                    </div>
                </div>

                <div className="space-y-2">
                    <label className="text-sm font-medium">Price</label>
                    <Input
                        name="price"
                        type="number"
                        step="0.01"
                        value={formData.price}
                        onChange={handleNumberChange}
                        placeholder="0.00"
                    />
                </div>

                <div className="space-y-2">
                    <label className="text-sm font-medium">Stock Quantity</label>
                    <Input
                        name="stockQuantity"
                        type="number"
                        value={formData.stockQuantity}
                        onChange={handleNumberChange}
                        placeholder="0"
                    />
                </div>

                <div className="space-y-2">
                    <div className="flex items-center space-x-2">
                        <Switch
                            id="isActive"
                            checked={formData.isActive}
                            onCheckedChange={(checked) => handleSwitchChange('isActive', checked)}
                        />
                        <label htmlFor="isActive" className="text-sm font-medium cursor-pointer">
                            Active
                        </label>
                    </div>
                </div>
            </div>

            {/* Variant Images Section */}
            <Card>
                <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                        <ImageIcon className="size-5"/>
                        Variant Images
                    </CardTitle>
                    <CardDescription>
                        Upload images specific to this variant. These images will be shown when this variant is
                        selected.
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <VariantImageUpload
                        images={variantImages}
                        onImagesChange={handleImagesChange}
                        onImageRemove={handleImageRemove}
                        isLoading={isLoading}
                    />
                </CardContent>
            </Card>

            {/* Attributes Display */}
            <div className="space-y-2">
                <label className="text-sm font-medium">Variant Attributes</label>
                {formData.attributes && formData.attributes.length > 0 && (
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        {formData.attributes.map(attr => {
                            const attribute = getAttributeById(attr.attributeId);
                            const hasError = attributeErrors[attr.attributeId];

                            return (
                                <div key={attr.attributeId} className="space-y-2">
                                    <div className="flex flex-col space-y-1">
                                        <span className="text-sm font-medium">
                                            {attribute?.displayName || 'Unknown Attribute'}
                                        </span>
                                        <Badge variant="secondary" className="text-xs w-fit">
                                            {attr.value ? formatAttributeValue(attr.value) : 'Not set'}
                                        </Badge>
                                        {hasError && (
                                            <span className="text-xs text-destructive flex items-center">
                                                <AlertCircle className="size-3 mr-1"/>
                                                {attributeErrors[attr.attributeId]}
                                            </span>
                                        )}
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                )}
            </div>

            {/* Form actions */}
            <div className="pt-4 flex justify-end space-x-2">
                <Button
                    type="button"
                    variant="outline"
                    onClick={(e) => {
                        e.preventDefault();
                        e.stopPropagation(); // Stop event propagation
                        onCancel();
                    }}
                    disabled={isLoading}
                >
                    Cancel
                </Button>
                <Button
                    type="submit"
                    disabled={isLoading || availableVariantAttributes.length === 0 || (!variant && !selectedCombination)}
                >
                    {isLoading && <Loader2 className="size-4 mr-2 animate-spin"/>}
                    {variant ? 'Update' : 'Create'} Variant
                </Button>
            </div>
        </form>
    );
};

export default VariantForm;