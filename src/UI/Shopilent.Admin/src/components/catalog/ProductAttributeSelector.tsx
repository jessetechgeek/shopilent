import React, {useState} from 'react';
import {PlusCircle, X, Search, Check} from 'lucide-react';
import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
    CardDescription,
} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {
    Command,
    CommandEmpty,
    CommandGroup,
    CommandInput,
    CommandItem,
} from '@/components/ui/command';
import {
    Popover,
    PopoverContent,
    PopoverTrigger,
} from '@/components/ui/popover';
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';
import {Switch} from '@/components/ui/switch';
import {Badge} from '@/components/ui/badge';
import {AttributeDto, AttributeType} from '@/models/catalog';

interface ProductAttributeValue {
    attributeId: string;
    value: any;
}

interface ProductAttributeSelectorProps {
    attributes: AttributeDto[];
    productAttributes: ProductAttributeValue[];
    onAttributesChange: (attributes: ProductAttributeValue[]) => void;
    isLoading?: boolean;
}

const ProductAttributeSelector: React.FC<ProductAttributeSelectorProps> = ({
                                                                               attributes,
                                                                               productAttributes,
                                                                               onAttributesChange,
                                                                               isLoading = false,
                                                                           }) => {
    const [searchQuery, setSearchQuery] = useState('');
    const [attributeDialogOpen, setAttributeDialogOpen] = useState(false);

    // Change this to a map of attribute IDs to input values
    const [inputValues, setInputValues] = useState<Record<string, string>>({});

    // Filter attributes based on search query
    const filteredAttributes = attributes.filter(attr =>
        !productAttributes.some(pa => pa.attributeId === attr.id) &&
        (attr.displayName.toLowerCase().includes(searchQuery.toLowerCase()) ||
            attr.name.toLowerCase().includes(searchQuery.toLowerCase()))
    );

    // Handle attribute selection
    const handleSelectAttribute = (attributeId: string) => {
        setAttributeDialogOpen(false);

        // Find the attribute
        const attribute = attributes.find(a => a.id === attributeId);
        if (!attribute) return;

        // Initialize with appropriate default value based on attribute type
        let defaultValue: any;
        switch (attribute.type) {
            case AttributeType.Text:
                defaultValue = '';
                break;
            case AttributeType.Number:
                defaultValue = 0;
                break;
            case AttributeType.Boolean:
                defaultValue = false;
                break;
            case AttributeType.Select:
                defaultValue = attribute.configuration?.values &&
                attribute.configuration.values.length > 0 ?
                    [attribute.configuration.values[0]] : [];
                break;
            case AttributeType.Color:
                defaultValue = attribute.configuration?.values &&
                attribute.configuration.values.length > 0 ?
                    [attribute.configuration.values[0].name] : [];
                break;
            case AttributeType.Date:
                defaultValue = [new Date().toISOString().split('T')[0]];
                break;
            case AttributeType.Dimensions:
            case AttributeType.Weight:
                defaultValue = [];
                break;
            default:
                defaultValue = '';
        }

        // Add the new attribute
        const updatedAttributes = [
            ...productAttributes,
            {attributeId, value: defaultValue}
        ];
        onAttributesChange(updatedAttributes);
    };

    // Handle attribute value change
    const handleAttributeValueChange = (attributeId: string, value: any) => {
        const updatedAttributes = productAttributes.map(attr =>
            attr.attributeId === attributeId ? {...attr, value} : attr
        );
        onAttributesChange(updatedAttributes);
    };

    // Handle attribute removal
    const handleRemoveAttribute = (attributeId: string) => {
        const updatedAttributes = productAttributes.filter(
            attr => attr.attributeId !== attributeId
        );
        onAttributesChange(updatedAttributes);

        // Also clear the input value for this attribute
        const newInputValues = {...inputValues};
        delete newInputValues[attributeId];
        setInputValues(newInputValues);
    };

    // Find attribute details by ID
    const getAttributeById = (attributeId: string) => {
        return attributes.find(attr => attr.id === attributeId);
    };

    // Handle multiple value addition for attributes
    const handleAddValueToArray = (attributeId: string, currentValues: any[], newValue: any) => {
        if (!newValue || (Array.isArray(currentValues) && currentValues.includes(newValue))) {
            return;
        }

        const newValues = Array.isArray(currentValues) ? [...currentValues, newValue] : [newValue];
        handleAttributeValueChange(attributeId, newValues);
    };

    // Handle value removal for multi-value attributes
    const handleRemoveValueFromArray = (attributeId: string, currentValues: any[], valueToRemove: any) => {
        if (!Array.isArray(currentValues)) return;

        const newValues = currentValues.filter(v => v !== valueToRemove);
        handleAttributeValueChange(attributeId, newValues);
    };

    // Update input value for a specific attribute
    const handleInputValueChange = (attributeId: string, value: string) => {
        setInputValues(prev => ({
            ...prev,
            [attributeId]: value
        }));
    };

    // Clear input value for a specific attribute
    const clearInputValue = (attributeId: string) => {
        setInputValues(prev => ({
            ...prev,
            [attributeId]: ''
        }));
    };

    // Render the input control based on attribute type
    const renderAttributeInput = (attributeId: string, possiblyNestedValue: any) => {
        const attribute = getAttributeById(attributeId);

        if (!attribute) return null;

        const currentValue = possiblyNestedValue?.value !== undefined ?
            possiblyNestedValue.value : possiblyNestedValue;

        switch (attribute.type) {
            case AttributeType.Text:
                return (
                    <Input
                        value={currentValue || ''}
                        onChange={(e) => handleAttributeValueChange(attributeId, e.target.value)}
                        placeholder={`Enter ${attribute.displayName.toLowerCase()}`}
                    />
                );

            case AttributeType.Number:
                return (
                    <div className="space-y-2">
                        <div className="flex items-center space-x-2">
                            <Input
                                type="number"
                                step="any"
                                value={inputValues[attributeId] || ''}
                                onChange={(e) => handleInputValueChange(attributeId, e.target.value)}
                                placeholder={`Add ${attribute.displayName.toLowerCase()}`}
                                onKeyDown={(e) => {
                                    if (e.key === 'Enter' && e.currentTarget.value.trim()) {
                                        e.preventDefault();
                                        const numValue = parseFloat(e.currentTarget.value);
                                        if (!isNaN(numValue)) {
                                            handleAddValueToArray(attributeId, currentValue || [], numValue);
                                            clearInputValue(attributeId);
                                        }
                                    }
                                }}
                            />
                            <Button
                                type="button"
                                size="sm"
                                onClick={() => {
                                    const inputValue = inputValues[attributeId];
                                    if (inputValue && !isNaN(parseFloat(inputValue))) {
                                        handleAddValueToArray(attributeId, currentValue || [], parseFloat(inputValue));
                                        clearInputValue(attributeId);
                                    }
                                }}
                            >
                                Add
                            </Button>
                            {attribute.configuration?.unit && (
                                <span className="text-sm text-muted-foreground">
                                    {attribute.configuration.unit}
                                </span>
                            )}
                        </div>

                        {/* Display selected values */}
                        {Array.isArray(currentValue) && currentValue.length > 0 && (
                            <div className="flex flex-wrap gap-1 mt-2">
                                {currentValue.map((value, index) => (
                                    <Badge
                                        key={index}
                                        variant="secondary"
                                        className="flex items-center gap-1"
                                    >
                                        {value}{attribute.configuration?.unit ? ` ${attribute.configuration.unit}` : ''}
                                        <X
                                            className="h-3 w-3 cursor-pointer"
                                            onClick={() => handleRemoveValueFromArray(attributeId, currentValue, value)}
                                        />
                                    </Badge>
                                ))}
                            </div>
                        )}
                    </div>
                );

            case AttributeType.Boolean:
                return (
                    <div className="flex items-center space-x-2">
                        <Switch
                            checked={!!currentValue}
                            onCheckedChange={(checked) => handleAttributeValueChange(attributeId, checked)}
                        />
                        <span className="text-sm">
                            {currentValue ? 'Yes' : 'No'}
                        </span>
                    </div>
                );

            case AttributeType.Select:
                if (attribute.configuration?.values) {
                    return (
                        <div className="space-y-2">
                            <Popover>
                                <PopoverTrigger asChild>
                                    <Button
                                        type="button"
                                        variant="outline"
                                        className="w-full justify-start"
                                    >
                                        <span>Select {attribute.displayName.toLowerCase()}</span>
                                    </Button>
                                </PopoverTrigger>
                                <PopoverContent className="w-full p-0" align="start">
                                    <Command>
                                        <CommandInput placeholder={`Search ${attribute.displayName.toLowerCase()}...`}/>
                                        <CommandEmpty>No options found.</CommandEmpty>
                                        <CommandGroup>
                                            {attribute.configuration.values.map((option: string) => (
                                                <CommandItem
                                                    key={option}
                                                    value={option}
                                                    onSelect={() => {
                                                        handleAddValueToArray(attributeId, currentValue || [], option);
                                                    }}
                                                >
                                                    <span>{option}</span>
                                                    {Array.isArray(currentValue) && currentValue.includes(option) && (
                                                        <Check className="h-4 w-4 ml-auto"/>
                                                    )}
                                                </CommandItem>
                                            ))}
                                        </CommandGroup>
                                    </Command>
                                </PopoverContent>
                            </Popover>

                            {/* Display selected values */}
                            {Array.isArray(currentValue) && currentValue.length > 0 && (
                                <div className="flex flex-wrap gap-1 mt-2">
                                    {currentValue.map((value, index) => (
                                        <Badge
                                            key={index}
                                            variant="secondary"
                                            className="flex items-center gap-1"
                                        >
                                            {value}
                                            <X
                                                className="h-3 w-3 cursor-pointer"
                                                onClick={() => handleRemoveValueFromArray(attributeId, currentValue, value)}
                                            />
                                        </Badge>
                                    ))}
                                </div>
                            )}
                        </div>
                    );
                }
                return null;

            case AttributeType.Color:
                if (attribute.configuration?.values) {
                    return (
                        <div className="space-y-2">
                            <Popover>
                                <PopoverTrigger asChild>
                                    <Button
                                        type="button"
                                        variant="outline"
                                        className="w-full justify-start"
                                    >
                                        <span>Select {attribute.displayName.toLowerCase()}</span>
                                    </Button>
                                </PopoverTrigger>
                                <PopoverContent className="w-full p-0" align="start">
                                    <Command>
                                        <CommandInput placeholder={`Search ${attribute.displayName.toLowerCase()}...`}/>
                                        <CommandEmpty>No colors found.</CommandEmpty>
                                        <CommandGroup>
                                            {attribute.configuration.values.map((color: {
                                                name: string;
                                                hex: string
                                            }) => (
                                                <CommandItem
                                                    key={color.name}
                                                    value={color.name}
                                                    onSelect={() => {
                                                        handleAddValueToArray(attributeId, currentValue || [], color.name);
                                                    }}
                                                    className="flex items-center gap-2"
                                                >
                                                    <div
                                                        className="w-4 h-4 rounded-full mr-2"
                                                        style={{backgroundColor: color.hex}}
                                                    />
                                                    <span>{color.name}</span>
                                                    {Array.isArray(currentValue) && currentValue.includes(color.name) && (
                                                        <Check className="h-4 w-4 ml-auto"/>
                                                    )}
                                                </CommandItem>
                                            ))}
                                        </CommandGroup>
                                    </Command>
                                </PopoverContent>
                            </Popover>

                            {/* Display selected values */}
                            {Array.isArray(currentValue) && currentValue.length > 0 && (
                                <div className="flex flex-wrap gap-1 mt-2">
                                    {currentValue.map((colorName, index) => {
                                        const colorObj = attribute.configuration?.values.find(
                                            (c: { name: string; hex: string }) => c.name === colorName
                                        );

                                        return (
                                            <Badge
                                                key={index}
                                                variant="outline"
                                                className="flex items-center gap-1"
                                            >
                                                <div
                                                    className="w-3 h-3 rounded-full"
                                                    style={{backgroundColor: colorObj?.hex || '#ccc'}}
                                                />
                                                {colorName}
                                                <X
                                                    className="h-3 w-3 cursor-pointer"
                                                    onClick={() => handleRemoveValueFromArray(attributeId, currentValue, colorName)}
                                                />
                                            </Badge>
                                        );
                                    })}
                                </div>
                            )}
                        </div>
                    );
                }
                return null;

            case AttributeType.Date:
                return (
                    <div className="space-y-2">
                        <div className="flex items-center space-x-2">
                            <Input
                                type="date"
                                value=""
                                onChange={(e) => {
                                    if (e.target.value) {
                                        handleAddValueToArray(attributeId, currentValue || [], e.target.value);
                                        e.target.value = '';
                                    }
                                }}
                            />
                        </div>

                        {/* Display selected values */}
                        {Array.isArray(currentValue) && currentValue.length > 0 && (
                            <div className="flex flex-wrap gap-1 mt-2">
                                {currentValue.map((value, index) => (
                                    <Badge
                                        key={index}
                                        variant="secondary"
                                        className="flex items-center gap-1"
                                    >
                                        {new Date(value).toLocaleDateString()}
                                        <X
                                            className="h-3 w-3 cursor-pointer"
                                            onClick={() => handleRemoveValueFromArray(attributeId, currentValue, value)}
                                        />
                                    </Badge>
                                ))}
                            </div>
                        )}
                    </div>
                );

            case AttributeType.Dimensions:
            case AttributeType.Weight:
                return (
                    <div className="space-y-2">
                        <div className="flex items-center space-x-2">
                            <Input
                                type="number"
                                step="any"
                                value={inputValues[attributeId] || ''}
                                onChange={(e) => handleInputValueChange(attributeId, e.target.value)}
                                placeholder={`Add ${attribute.displayName.toLowerCase()}`}
                                onKeyDown={(e) => {
                                    if (e.key === 'Enter' && e.currentTarget.value.trim()) {
                                        e.preventDefault();
                                        const numValue = parseFloat(e.currentTarget.value);
                                        if (!isNaN(numValue)) {
                                            handleAddValueToArray(attributeId, currentValue || [], numValue);
                                            clearInputValue(attributeId);
                                        }
                                    }
                                }}
                            />
                            <Button
                                type="button"
                                size="sm"
                                onClick={() => {
                                    const inputValue = inputValues[attributeId];
                                    if (inputValue && !isNaN(parseFloat(inputValue))) {
                                        handleAddValueToArray(attributeId, currentValue || [], parseFloat(inputValue));
                                        clearInputValue(attributeId);
                                    }
                                }}
                            >
                                Add
                            </Button>
                            {attribute.configuration?.unit && (
                                <span className="text-sm text-muted-foreground">
                                    {attribute.configuration.unit}
                                </span>
                            )}
                        </div>

                        {/* Display selected values */}
                        {Array.isArray(currentValue) && currentValue.length > 0 && (
                            <div className="flex flex-wrap gap-1 mt-2">
                                {currentValue.map((value, index) => (
                                    <Badge
                                        key={index}
                                        variant="secondary"
                                        className="flex items-center gap-1"
                                    >
                                        {value}{attribute.configuration?.unit ? ` ${attribute.configuration.unit}` : ''}
                                        <X
                                            className="h-3 w-3 cursor-pointer"
                                            onClick={() => handleRemoveValueFromArray(attributeId, currentValue, value)}
                                        />
                                    </Badge>
                                ))}
                            </div>
                        )}
                    </div>
                );

            default:
                return (
                    <Input
                        value={currentValue || ''}
                        onChange={(e) => handleAttributeValueChange(attributeId, e.target.value)}
                        placeholder={`Enter ${attribute.displayName.toLowerCase()}`}
                    />
                );
        }
    };

    return (
        <Card>
            <CardHeader className="flex flex-row items-center justify-between">
                <div>
                    <CardTitle>Product Attributes</CardTitle>
                    <CardDescription>
                        Add and configure attributes for this product.
                    </CardDescription>
                </div>
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => setAttributeDialogOpen(true)}
                    disabled={isLoading || filteredAttributes.length === 0}
                >
                    <PlusCircle className="size-4 mr-2"/>
                    Add Attribute
                </Button>
            </CardHeader>
            <CardContent>
                {isLoading ? (
                    <div className="flex items-center justify-center py-4 text-sm text-muted-foreground">
                        Loading attributes...
                    </div>
                ) : productAttributes.length === 0 ? (
                    <div className="text-center py-8 border rounded-md border-dashed">
                        <p className="text-muted-foreground">No attributes assigned to this product</p>
                        <Button
                            type="button"
                            variant="secondary"
                            size="sm"
                            className="mt-2"
                            onClick={() => setAttributeDialogOpen(true)}
                            disabled={filteredAttributes.length === 0}
                        >
                            <PlusCircle className="size-4 mr-2"/>
                            Add Attribute
                        </Button>
                    </div>
                ) : (
                    <div className="space-y-4">
                        {productAttributes.map((attr) => {
                            const attributeDetails = getAttributeById(attr.attributeId);
                            if (!attributeDetails) return null;

                            return (
                                <div
                                    key={attr.attributeId}
                                    className="p-3 border rounded-md shadow-sm"
                                >
                                    <div className="flex justify-between items-center mb-2">
                                        <div className="flex items-center gap-2">
                                            <h4 className="font-medium text-sm">
                                                {attributeDetails.displayName}
                                            </h4>
                                            <Badge variant="outline" className="text-xs">
                                                {attributeDetails.type}
                                            </Badge>
                                        </div>
                                        <Button
                                            variant="ghost"
                                            size="sm"
                                            className="h-6 w-6 p-0 text-muted-foreground hover:text-destructive"
                                            onClick={() => handleRemoveAttribute(attr.attributeId)}
                                        >
                                            <X className="size-4"/>
                                        </Button>
                                    </div>
                                    {renderAttributeInput(attr.attributeId, attr.value)}
                                </div>
                            );
                        })}
                    </div>
                )}

                {/* Add Attribute Dialog */}
                <Dialog open={attributeDialogOpen} onOpenChange={setAttributeDialogOpen}>
                    <DialogContent className="sm:max-w-[500px]">
                        <DialogHeader>
                            <DialogTitle>Add Product Attribute</DialogTitle>
                            <DialogDescription>
                                Select an attribute to add to this product.
                            </DialogDescription>
                        </DialogHeader>

                        <div className="py-4">
                            <div className="relative mb-4">
                                <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground"/>
                                <Input
                                    placeholder="Search attributes..."
                                    className="pl-8"
                                    value={searchQuery}
                                    onChange={(e) => setSearchQuery(e.target.value)}
                                />
                            </div>

                            <div className="max-h-[300px] overflow-y-auto border rounded-md">
                                {filteredAttributes.length === 0 ? (
                                    <div className="p-4 text-center text-muted-foreground">
                                        {searchQuery
                                            ? "No matching attributes found"
                                            : "All available attributes have been added"}
                                    </div>
                                ) : (
                                    <div className="divide-y">
                                        {filteredAttributes.map((attribute) => (
                                            <div
                                                key={attribute.id}
                                                className="p-3 hover:bg-muted cursor-pointer flex items-center justify-between"
                                                onClick={() => handleSelectAttribute(attribute.id)}
                                            >
                                                <div>
                                                    <div className="font-medium">{attribute.displayName}</div>
                                                    <div className="text-sm text-muted-foreground">
                                                        {attribute.name} - {attribute.type}
                                                    </div>
                                                </div>
                                                <PlusCircle className="h-5 w-5 text-primary"/>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>
                        </div>

                        <DialogFooter>
                            <Button variant="outline" onClick={() => setAttributeDialogOpen(false)}>
                                Cancel
                            </Button>
                        </DialogFooter>
                    </DialogContent>
                </Dialog>
            </CardContent>
        </Card>
    );
};

export default ProductAttributeSelector;