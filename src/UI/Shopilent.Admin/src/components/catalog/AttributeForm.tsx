// import React, {useState, useEffect} from 'react';
// import {Button} from '@/components/ui/button';
// import {Input} from '@/components/ui/input';
// import {
//     Select,
//     SelectContent,
//     SelectItem,
//     SelectTrigger,
//     SelectValue,
// } from '@/components/ui/select';
// import {Switch} from '@/components/ui/switch';
// import {Textarea} from '@/components/ui/textarea';
// import {Loader2, Plus, Trash2, Dimensions} from 'lucide-react';
// import {
//     Card,
//     CardContent,
// } from '@/components/ui/card';
// import {
//     Tabs,
//     TabsContent,
//     TabsList,
//     TabsTrigger,
// } from '@/components/ui/tabs';
// import {
//     AttributeDto,
//     CreateAttributeRequest,
//     UpdateAttributeRequest,
//     AttributeType
// } from '@/models/catalog';
// import {attributeApi} from '@/api/attributes';
// import {useQuery} from '@tanstack/react-query';
//
// interface AttributeFormProps {
//     attributeId?: string;
//     onSubmit: (attributeData: CreateAttributeRequest | UpdateAttributeRequest) => Promise<void>;
//     onCancel: () => void;
//     isLoading: boolean;
// }
//
// const AttributeForm: React.FC<AttributeFormProps> = ({
//                                                          attributeId,
//                                                          onSubmit,
//                                                          onCancel,
//                                                          isLoading
//                                                      }) => {
//     const [formData, setFormData] = useState<CreateAttributeRequest>({
//         name: '',
//         displayName: '',
//         type: AttributeType.Text,
//         configuration: {},
//         filterable: false,
//         searchable: false,
//         isVariant: false
//     });
//
//     const [selectValues, setSelectValues] = useState<string[]>([]);
//     const [newValue, setNewValue] = useState('');
//     const [displayType, setDisplayType] = useState('dropdown');
//
//     // For color attributes
//     const [colorCodes, setColorCodes] = useState<Record<string, string>>({});
//
//     // Fetch attribute data if editing
//     const {data: attributeData, isLoading: isLoadingAttribute} = useQuery({
//         queryKey: ['attribute', attributeId],
//         queryFn: async () => {
//             if (!attributeId) return null;
//             const response = await attributeApi.getAttributeById(attributeId);
//             if (response.data.succeeded) {
//                 return response.data.data;
//             }
//             throw new Error(response.data.message || 'Failed to fetch attribute');
//         },
//         enabled: !!attributeId
//     });
//
//     // Load form data when editing existing attribute
//     useEffect(() => {
//         if (attributeData) {
//             setFormData({
//                 name: attributeData.name,
//                 displayName: attributeData.displayName,
//                 type: attributeData.type,
//                 configuration: attributeData.configuration || {},
//                 filterable: attributeData.filterable,
//                 searchable: attributeData.searchable,
//                 isVariant: attributeData.isVariant
//             });
//
//             // Handle select values
//             if (attributeData.type === AttributeType.Select && attributeData.configuration?.values) {
//                 setSelectValues(attributeData.configuration.values || []);
//                 setDisplayType(attributeData.configuration.displayType || 'dropdown');
//
//                 // Load color codes if they exist
//                 if (attributeData.configuration.colorCodes) {
//                     setColorCodes(attributeData.configuration.colorCodes);
//                 }
//             }
//         }
//     }, [attributeData]);
//
//     // Helper to get the string representation of AttributeType enum value
//     const getAttributeTypeString = (type: AttributeType): string => {
//         return AttributeType[type];
//     };
//
//     // Handle form input changes
//     const handleChange = (
//         e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
//     ) => {
//         const {name, value} = e.target;
//         setFormData(prev => ({...prev, [name]: value}));
//     };
//
//     // Handle type selection
//     const handleTypeChange = (value: string) => {
//         const enumValue = parseInt(value);
//         setFormData(prev => ({
//             ...prev,
//             type: enumValue,
//             // Reset configuration when type changes
//             configuration: {}
//         }));
//
//         // Reset configuration-specific states
//         setSelectValues([]);
//         setColorCodes({});
//         setDisplayType('dropdown');
//     };
//
//     // Handle switchable options
//     const handleSwitchChange = (name: string, checked: boolean) => {
//         setFormData(prev => ({...prev, [name]: checked}));
//     };
//
//     // Handle select value addition
//     const handleAddValue = () => {
//         if (!newValue.trim()) return;
//
//         if (selectValues.includes(newValue.trim())) {
//             // Value already exists
//             return;
//         }
//
//         const updatedValues = [...selectValues, newValue.trim()];
//         setSelectValues(updatedValues);
//
//         // Initialize color code if attribute is a color
//         if (formData.name === 'color' || displayType === 'swatch') {
//             setColorCodes(prev => ({
//                 ...prev,
//                 [newValue.trim()]: '#CCCCCC' // Default color
//             }));
//         }
//
//         setNewValue('');
//     };
//
//     // Handle select value removal
//     const handleRemoveValue = (value: string) => {
//         const updatedValues = selectValues.filter(v => v !== value);
//         setSelectValues(updatedValues);
//
//         // Remove from color codes if they exist
//         if (formData.name === 'color' || displayType === 'swatch') {
//             const updatedColorCodes = {...colorCodes};
//             delete updatedColorCodes[value];
//             setColorCodes(updatedColorCodes);
//         }
//     };
//
//     // Handle color code change
//     const handleColorCodeChange = (value: string, colorCode: string) => {
//         setColorCodes(prev => ({
//             ...prev,
//             [value]: colorCode
//         }));
//     };
//
//     // Handle display type change
//     const handleDisplayTypeChange = (type: string) => {
//         setDisplayType(type);
//
//         // Initialize color codes for all values if switching to swatch
//         if (type === 'swatch') {
//             const initialColorCodes: Record<string, string> = {};
//             selectValues.forEach(value => {
//                 initialColorCodes[value] = colorCodes[value] || '#CCCCCC';
//             });
//             setColorCodes(initialColorCodes);
//         }
//     };
//
//     // Auto-generate system name from display name
//     const handleDisplayNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
//         const displayName = e.target.value;
//
//         // Only auto-generate name if we're creating a new attribute and name hasn't been manually changed
//         if (!attributeId) {
//             const name = displayName
//                 .toLowerCase()
//                 .replace(/[^\w\s-]/g, '')
//                 .replace(/\s+/g, '_');
//
//             setFormData(prev => ({...prev, displayName, name}));
//         } else {
//             setFormData(prev => ({...prev, displayName}));
//         }
//     };
//
//     // Handle number field configuration changes
//     const handleNumberConfigChange = (field: string, value: string) => {
//         const numValue = value === '' ? undefined : parseFloat(value);
//         setFormData(prev => ({
//             ...prev,
//             configuration: {
//                 ...prev.configuration,
//                 [field]: numValue
//             }
//         }));
//     };
//
//     // Handle units for number fields
//     const handleUnitChange = (value: string) => {
//         setFormData(prev => ({
//             ...prev,
//             configuration: {
//                 ...prev.configuration,
//                 unit: value || undefined
//             }
//         }));
//     };
//
//     // Handle dimensions fields
//     const handleDimensionsChange = (field: string, value: string) => {
//         setFormData(prev => ({
//             ...prev,
//             configuration: {
//                 ...prev.configuration,
//                 [field]: value || undefined
//             }
//         }));
//     };
//
//     // Handle form submission
//     const handleSubmit = async (e: React.FormEvent) => {
//         e.preventDefault();
//
//         // Prepare configuration based on attribute type
//         let configuration = {...formData.configuration};
//
//         if (formData.type === AttributeType.Select) {
//             configuration = {
//                 ...configuration,
//                 values: selectValues,
//                 displayType: displayType
//             };
//
//             // Add color codes if it's a color attribute or using swatches
//             if (formData.name === 'color' || displayType === 'swatch') {
//                 configuration.colorCodes = colorCodes;
//             }
//         }
//
//         const updatedFormData = {
//             ...formData,
//             configuration
//         };
//
//         if (attributeId) {
//             // When editing, extract only the fields that should be in the UpdateAttributeRequest
//             const updateData: UpdateAttributeRequest = {
//                 displayName: updatedFormData.displayName,
//                 configuration: updatedFormData.configuration,
//                 filterable: updatedFormData.filterable,
//                 searchable: updatedFormData.searchable,
//                 isVariant: updatedFormData.isVariant
//             };
//             await onSubmit(updateData);
//         } else {
//             // For new attribute, send the full CreateAttributeRequest
//             await onSubmit(updatedFormData);
//         }
//     };
//
//     if (isLoadingAttribute) {
//         return (
//             <div className="flex justify-center items-center py-8">
//                 <Loader2 className="h-8 w-8 animate-spin text-primary"/>
//                 <span className="ml-2">Loading attribute data...</span>
//             </div>
//         );
//     }
//
//     return (
//         <form onSubmit={handleSubmit} className="space-y-4">
//             {/* Display Name field */}
//             <div className="space-y-2">
//                 <label className="text-sm font-medium" htmlFor="displayName">
//                     Display Name <span className="text-destructive">*</span>
//                 </label>
//                 <Input
//                     id="displayName"
//                     name="displayName"
//                     value={formData.displayName}
//                     onChange={handleDisplayNameChange}
//                     placeholder="Size, Color, Material, etc."
//                     required
//                 />
//                 <p className="text-xs text-muted-foreground">
//                     The name shown to users in the UI.
//                 </p>
//             </div>
//
//             {/* System Name field - Only show when creating a new attribute */}
//             {!attributeId && (
//                 <div className="space-y-2">
//                     <label className="text-sm font-medium" htmlFor="name">
//                         System Name <span className="text-destructive">*</span>
//                     </label>
//                     <Input
//                         id="name"
//                         name="name"
//                         value={formData.name}
//                         onChange={handleChange}
//                         placeholder="size, color, material, etc."
//                         required
//                     />
//                     <p className="text-xs text-muted-foreground">
//                         Internal name used in the system. Cannot be changed after creation.
//                     </p>
//                 </div>
//             )}
//
//             {/* Type field - Only show when creating a new attribute */}
//             {!attributeId && (
//                 <div className="space-y-2">
//                     <label className="text-sm font-medium">
//                         Type <span className="text-destructive">*</span>
//                     </label>
//                     <Select
//                         value={formData.type.toString()}
//                         onValueChange={handleTypeChange}
//                     >
//                         <SelectTrigger>
//                             <SelectValue placeholder="Select attribute type">
//                                 {formData.type !== undefined && getAttributeTypeString(formData.type)}
//                             </SelectValue>
//                         </SelectTrigger>
//                         <SelectContent>
//                             {Object.keys(AttributeType)
//                                 .filter(key => !isNaN(Number(key)))
//                                 .map(key => (
//                                     <SelectItem key={key} value={key}>
//                                         {AttributeType[Number(key)]}
//                                     </SelectItem>
//                                 ))}
//                         </SelectContent>
//                     </Select>
//                     <p className="text-xs text-muted-foreground">
//                         Determines how the attribute is displayed and used. Cannot be changed after creation.
//                     </p>
//                 </div>
//             )}
//
//             {/* If editing, display the type as read-only text */}
//             {attributeId && (
//                 <div className="space-y-2">
//                     <label className="text-sm font-medium">Type</label>
//                     <div className="p-2 border rounded-md bg-muted/50">
//                         {getAttributeTypeString(formData.type)}
//                     </div>
//                     <p className="text-xs text-muted-foreground">
//                         Attribute type cannot be changed after creation.
//                     </p>
//                 </div>
//             )}
//
//             {/* Text Attribute Configuration */}
//             {formData.type === AttributeType.Text && (
//                 <div className="space-y-2">
//                     <label className="text-sm font-medium">Text Validation</label>
//                     <div className="flex flex-col space-y-4">
//                         <div className="flex items-center space-x-2">
//                             <Switch
//                                 id="minLength"
//                                 checked={!!formData.configuration?.minLength}
//                                 onCheckedChange={(checked) => {
//                                     setFormData(prev => ({
//                                         ...prev,
//                                         configuration: {
//                                             ...prev.configuration,
//                                             minLength: checked ? 1 : undefined
//                                         }
//                                     }));
//                                 }}
//                             />
//                             <label htmlFor="minLength" className="text-sm font-medium cursor-pointer">
//                                 Minimum Length
//                             </label>
//                             {formData.configuration?.minLength !== undefined && (
//                                 <Input
//                                     type="number"
//                                     value={formData.configuration.minLength || 1}
//                                     min={1}
//                                     onChange={(e) => {
//                                         setFormData(prev => ({
//                                             ...prev,
//                                             configuration: {
//                                                 ...prev.configuration,
//                                                 minLength: parseInt(e.target.value) || 1
//                                             }
//                                         }));
//                                     }}
//                                     className="w-20"
//                                 />
//                             )}
//                         </div>
//
//                         <div className="flex items-center space-x-2">
//                             <Switch
//                                 id="maxLength"
//                                 checked={!!formData.configuration?.maxLength}
//                                 onCheckedChange={(checked) => {
//                                     setFormData(prev => ({
//                                         ...prev,
//                                         configuration: {
//                                             ...prev.configuration,
//                                             maxLength: checked ? 100 : undefined
//                                         }
//                                     }));
//                                 }}
//                             />
//                             <label htmlFor="maxLength" className="text-sm font-medium cursor-pointer">
//                                 Maximum Length
//                             </label>
//                             {formData.configuration?.maxLength !== undefined && (
//                                 <Input
//                                     type="number"
//                                     value={formData.configuration.maxLength || 100}
//                                     min={1}
//                                     onChange={(e) => {
//                                         setFormData(prev => ({
//                                             ...prev,
//                                             configuration: {
//                                                 ...prev.configuration,
//                                                 maxLength: parseInt(e.target.value) || 100
//                                             }
//                                         }));
//                                     }}
//                                     className="w-20"
//                                 />
//                             )}
//                         </div>
//                     </div>
//                 </div>
//             )}
//
//             {/* Number Attribute Configuration */}
//             {formData.type === AttributeType.Number && (
//                 <div className="space-y-4">
//                     <div className="grid grid-cols-2 gap-4">
//                         <div className="space-y-2">
//                             <label className="text-sm font-medium">Minimum Value</label>
//                             <Input
//                                 type="number"
//                                 value={formData.configuration?.min ?? ''}
//                                 onChange={(e) => handleNumberConfigChange('min', e.target.value)}
//                                 placeholder="Minimum value"
//                                 step="any"
//                             />
//                         </div>
//
//                         <div className="space-y-2">
//                             <label className="text-sm font-medium">Maximum Value</label>
//                             <Input
//                                 type="number"
//                                 value={formData.configuration?.max ?? ''}
//                                 onChange={(e) => handleNumberConfigChange('max', e.target.value)}
//                                 placeholder="Maximum value"
//                                 step="any"
//                             />
//                         </div>
//                     </div>
//
//                     <div className="grid grid-cols-2 gap-4">
//                         <div className="space-y-2">
//                             <label className="text-sm font-medium">Step</label>
//                             <Input
//                                 type="number"
//                                 value={formData.configuration?.step ?? ''}
//                                 onChange={(e) => handleNumberConfigChange('step', e.target.value)}
//                                 placeholder="Step size"
//                                 step="any"
//                             />
//                             <p className="text-xs text-muted-foreground">
//                                 Increment/decrement size (e.g., 0.01, 1, 5)
//                             </p>
//                         </div>
//
//                         <div className="space-y-2">
//                             <label className="text-sm font-medium">Unit</label>
//                             <Select
//                                 value={formData.configuration?.unit || ''}
//                                 onValueChange={handleUnitChange}
//                             >
//                                 <SelectTrigger>
//                                     <SelectValue placeholder="Select a unit"/>
//                                 </SelectTrigger>
//                                 <SelectContent>
//                                     <SelectItem value="">None</SelectItem>
//                                     <SelectItem value="kg">Kilograms (kg)</SelectItem>
//                                     <SelectItem value="g">Grams (g)</SelectItem>
//                                     <SelectItem value="lb">Pounds (lb)</SelectItem>
//                                     <SelectItem value="cm">Centimeters (cm)</SelectItem>
//                                     <SelectItem value="m">Meters (m)</SelectItem>
//                                     <SelectItem value="inches">Inches (in)</SelectItem>
//                                     <SelectItem value="ft">Feet (ft)</SelectItem>
//                                     <SelectItem value="L">Liters (L)</SelectItem>
//                                     <SelectItem value="ml">Milliliters (ml)</SelectItem>
//                                     <SelectItem value="W">Watts (W)</SelectItem>
//                                     <SelectItem value="hours">Hours (hrs)</SelectItem>
//                                 </SelectContent>
//                             </Select>
//                         </div>
//                     </div>
//                 </div>
//             )}
//
//             {/* Dimensions Attribute Configuration */}
//             {formData.type === AttributeType.Dimensions && (
//                 <div className="space-y-4">
//                     <h3 className="text-sm font-medium">Dimension Fields</h3>
//                     <div className="grid grid-cols-3 gap-4">
//                         <div className="space-y-2">
//                             <label className="text-xs font-medium">Length</label>
//                             <Input
//                                 type="text"
//                                 value={formData.configuration?.length ?? ''}
//                                 onChange={(e) => handleDimensionsChange('length', e.target.value)}
//                                 placeholder="Length"
//                             />
//                         </div>
//                         <div className="space-y-2">
//                             <label className="text-xs font-medium">Width</label>
//                             <Input
//                                 type="text"
//                                 value={formData.configuration?.width ?? ''}
//                                 onChange={(e) => handleDimensionsChange('width', e.target.value)}
//                                 placeholder="Width"
//                             />
//                         </div>
//                         <div className="space-y-2">
//                             <label className="text-xs font-medium">Height</label>
//                             <Input
//                                 type="text"
//                                 value={formData.configuration?.height ?? ''}
//                                 onChange={(e) => handleDimensionsChange('height', e.target.value)}
//                                 placeholder="Height"
//                             />
//                         </div>
//                     </div>
//                     <div className="space-y-2">
//                         <label className="text-sm font-medium">Unit</label>
//                         <Select
//                             value={formData.configuration?.unit || 'cm'}
//                             onValueChange={handleUnitChange}
//                         >
//                             <SelectTrigger>
//                                 <SelectValue placeholder="Select a unit"/>
//                             </SelectTrigger>
//                             <SelectContent>
//                                 <SelectItem value="cm">Centimeters (cm)</SelectItem>
//                                 <SelectItem value="m">Meters (m)</SelectItem>
//                                 <SelectItem value="inches">Inches (in)</SelectItem>
//                                 <SelectItem value="ft">Feet (ft)</SelectItem>
//                             </SelectContent>
//                         </Select>
//                     </div>
//                 </div>
//             )}
//
//             {/* Boolean Attribute Configuration */}
//             {formData.type === AttributeType.Boolean && (
//                 <div className="space-y-2">
//                     <label className="text-sm font-medium">Display Format</label>
//                     <Select
//                         value={formData.configuration?.format || 'switch'}
//                         onValueChange={(value) => {
//                             setFormData(prev => ({
//                                 ...prev,
//                                 configuration: {
//                                     ...prev.configuration,
//                                     format: value
//                                 }
//                             }));
//                         }}
//                     >
//                         <SelectTrigger>
//                             <SelectValue placeholder="Select display format"/>
//                         </SelectTrigger>
//                         <SelectContent>
//                             <SelectItem value="switch">Switch</SelectItem>
//                             <SelectItem value="checkbox">Checkbox</SelectItem>
//                             <SelectItem value="yes-no">Yes/No</SelectItem>
//                             <SelectItem value="true-false">True/False</SelectItem>
//                         </SelectContent>
//                     </Select>
//
//                     <div className="space-y-2 mt-4">
//                         <label className="text-sm font-medium">Default Value</label>
//                         <div className="flex items-center space-x-2">
//                             <Switch
//                                 id="defaultValue"
//                                 checked={!!formData.configuration?.defaultValue}
//                                 onCheckedChange={(checked) => {
//                                     setFormData(prev => ({
//                                         ...prev,
//                                         configuration: {
//                                             ...prev.configuration,
//                                             defaultValue: checked
//                                         }
//                                     }));
//                                 }}
//                             />
//                             <label htmlFor="defaultValue" className="text-sm cursor-pointer">
//                                 {formData.configuration?.defaultValue ? 'True' : 'False'}
//                             </label>
//                         </div>
//                     </div>
//                 </div>
//             )}
//
//             {/* Select Attribute Configuration */}
//             {formData.type === AttributeType.Select && (
//                 <div className="space-y-4">
//                     <div className="space-y-2">
//                         <label className="text-sm font-medium">Display Type</label>
//                         <Select
//                             value={displayType}
//                             onValueChange={handleDisplayTypeChange}
//                         >
//                             <SelectTrigger>
//                                 <SelectValue placeholder="Select display type"/>
//                             </SelectTrigger>
//                             <SelectContent>
//                                 <SelectItem value="dropdown">Dropdown</SelectItem>
//                                 <SelectItem value="button">Button Group</SelectItem>
//                                 <SelectItem value="swatch">Color Swatch</SelectItem>
//                                 <SelectItem value="radio">Radio Buttons</SelectItem>
//                             </SelectContent>
//                         </Select>
//                         <p className="text-xs text-muted-foreground">
//                             How this attribute will be displayed in the product page.
//                         </p>
//                     </div>
//
//                     <div className="space-y-2">
//                         <label className="text-sm font-medium">Options</label>
//                         <div className="flex space-x-2">
//                             <Input
//                                 value={newValue}
//                                 onChange={(e) => setNewValue(e.target.value)}
//                                 placeholder="Enter option value"
//                                 onKeyDown={(e) => {
//                                     if (e.key === 'Enter') {
//                                         e.preventDefault();
//                                         handleAddValue();
//                                     }
//                                 }}
//                             />
//                             <Button
//                                 type="button"
//                                 onClick={handleAddValue}
//                                 size="icon"
//                             >
//                                 <Plus className="size-4"/>
//                             </Button>
//                         </div>
//
//                         {selectValues.length > 0 && (
//                             <Card className="mt-4">
//                                 <CardContent className="p-4">
//                                     <Tabs defaultValue="options">
//                                         <TabsList className="mb-4">
//                                             <TabsTrigger value="options">Options</TabsTrigger>
//                                             {(formData.name === 'color' || displayType === 'swatch') &&
//                                                 <TabsTrigger value="colors">Colors</TabsTrigger>
//                                             }
//                                         </TabsList>
//
//                                         <TabsContent value="options" className="space-y-2">
//                                             {selectValues.map((value) => (
//                                                 <div key={value}
//                                                      className="flex items-center justify-between p-2 border rounded-md">
//                                                     <span>{value}</span>
//                                                     <Button
//                                                         type="button"
//                                                         variant="ghost"
//                                                         size="icon"
//                                                         onClick={() => handleRemoveValue(value)}
//                                                     >
//                                                         <Trash2 className="size-4 text-destructive"/>
//                                                     </Button>
//                                                 </div>
//                                             ))}
//                                         </TabsContent>
//
//                                         {(formData.name === 'color' || displayType === 'swatch') && (
//                                             <TabsContent value="colors" className="space-y-2">
//                                                 {selectValues.map((value) => (
//                                                     <div key={value}
//                                                          className="flex items-center space-x-2 p-2 border rounded-md">
//                                                         <div
//                                                             className="size-6 rounded-full border"
//                                                             style={{backgroundColor: colorCodes[value] || '#CCCCCC'}}
//                                                         />
//                                                         <span className="flex-1">{value}</span>
//                                                         <Input
//                                                             type="color"
//                                                             value={colorCodes[value] || '#CCCCCC'}
//                                                             onChange={(e) => handleColorCodeChange(value, e.target.value)}
//                                                             className="w-14"
//                                                         />
//                                                         <Input
//                                                             type="text"
//                                                             value={colorCodes[value] || '#CCCCCC'}
//                                                             onChange={(e) => handleColorCodeChange(value, e.target.value)}
//                                                             className="w-24"
//                                                         />
//                                                     </div>
//                                                 ))}
//                                             </TabsContent>
//                                         )}
//                                     </Tabs>
//                                 </CardContent>
//                             </Card>
//                         )}
//
//                         {selectValues.length === 0 && (
//                             <p className="text-xs text-muted-foreground italic mt-2">
//                                 No options added yet. Add at least one option.
//                             </p>
//                         )}
//                     </div>
//                 </div>
//             )}
//
//             {/* Variant switch */}
//             <div className="flex items-center space-x-2">
//                 <Switch
//                     id="isVariant"
//                     checked={formData.isVariant}
//                     onCheckedChange={(checked) => handleSwitchChange('isVariant', checked)}
//                 />
//                 <label htmlFor="isVariant" className="text-sm font-medium cursor-pointer">
//                     Use for variants
//                 </label>
//                 <p className="text-xs text-muted-foreground ml-2">
//                     Attributes used for variants create different product options (e.g., different sizes, colors).
//                 </p>
//             </div>
//
//             {/* Filterable switch */}
//             <div className="flex items-center space-x-2">
//                 <Switch
//                     id="filterable"
//                     checked={formData.filterable}
//                     onCheckedChange={(checked) => handleSwitchChange('filterable', checked)}
//                 />
//                 <label htmlFor="filterable" className="text-sm font-medium cursor-pointer">
//                     Filterable
//                 </label>
//                 <p className="text-xs text-muted-foreground ml-2">
//                     Allow customers to filter products by this attribute.
//                 </p>
//             </div>
//
//             {/* Searchable switch */}
//             <div className="flex items-center space-x-2">
//                 <Switch
//                     id="searchable"
//                     checked={formData.searchable}
//                     onCheckedChange={(checked) => handleSwitchChange('searchable', checked)}
//                 />
//                 <label htmlFor="searchable" className="text-sm font-medium cursor-pointer">
//                     Searchable
//                 </label>
//                 <p className="text-xs text-muted-foreground ml-2">
//                     Include this attribute in product search.
//                 </p>
//             </div>
//
//             {/* Form actions */}
//             <div className="pt-4 flex justify-end space-x-2">
//                 <Button type="button" variant="outline" onClick={onCancel} disabled={isLoading}>
//                     Cancel
//                 </Button>
//                 <Button
//                     type="submit"
//                     disabled={
//                         isLoading ||
//                         (formData.type === AttributeType.Select && selectValues.length === 0)
//                     }
//                 >
//                     {isLoading && <Loader2 className="size-4 mr-2 animate-spin"/>}
//                     {attributeId ? 'Update' : 'Create'} Attribute
//                 </Button>
//             </div>
//         </form>
//     );
// };
//
// export default AttributeForm;


import React, {useState, useEffect} from 'react';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';
import {Switch} from '@/components/ui/switch';
import {Loader2, Plus, X} from 'lucide-react';
import {
    Card,
    CardContent,
} from '@/components/ui/card';
import {
    CreateAttributeRequest,
    UpdateAttributeRequest,
    AttributeType
} from '@/models/catalog';
import {attributeApi} from '@/api/attributes';
import {useQuery} from '@tanstack/react-query';

interface AttributeFormProps {
    attributeId?: string;
    onSubmit: (attributeData: CreateAttributeRequest | UpdateAttributeRequest) => Promise<void>;
    onCancel: () => void;
    isLoading: boolean;
}

const AttributeForm: React.FC<AttributeFormProps> = ({
                                                         attributeId,
                                                         onSubmit,
                                                         onCancel,
                                                         isLoading
                                                     }) => {
    const [formData, setFormData] = useState<CreateAttributeRequest>({
        name: '',
        displayName: '',
        type: AttributeType.Text,
        configuration: {},
        filterable: false,
        searchable: false,
        isVariant: false
    });

    const [selectValues, setSelectValues] = useState<string[]>([]);
    const [newValue, setNewValue] = useState('');

    // For color attributes
    const [colorValues, setColorValues] = useState<Array<{ name: string, hex: string }>>([]);
    const [newColorName, setNewColorName] = useState('');
    const [newColorHex, setNewColorHex] = useState('#000000');

    // Fetch attribute data if editing
    const {data: attributeData, isLoading: isLoadingAttribute} = useQuery({
        queryKey: ['attribute', attributeId],
        queryFn: async () => {
            if (!attributeId) return null;
            const response = await attributeApi.getAttributeById(attributeId);
            if (response.data.succeeded) {
                return response.data.data;
            }
            throw new Error(response.data.message || 'Failed to fetch attribute');
        },
        enabled: !!attributeId
    });

    // Load form data when editing existing attribute
    useEffect(() => {
        if (attributeData) {
            setFormData({
                name: attributeData.name,
                displayName: attributeData.displayName,
                type: attributeData.type,
                configuration: attributeData.configuration || {},
                filterable: attributeData.filterable,
                searchable: attributeData.searchable,
                isVariant: attributeData.isVariant
            });

            // Handle configuration values based on type
            if (attributeData.type === AttributeType.Select && attributeData.configuration?.values) {
                setSelectValues(attributeData.configuration.values || []);
            } else if (attributeData.type === AttributeType.Color && attributeData.configuration?.values) {
                setColorValues(attributeData.configuration.values || []);
            }
        }
    }, [attributeData]);

    // Handle form input changes
    const handleChange = (
        e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
    ) => {
        const {name, value} = e.target;
        setFormData(prev => ({...prev, [name]: value}));
    };

    // Handle type selection
    const handleTypeChange = (value: string) => {
        setFormData(prev => ({
            ...prev,
            type: value as AttributeType,
            // Reset configuration when type changes
            configuration: {}
        }));

        // Reset configuration-specific states
        setSelectValues([]);
        setColorValues([]);
    };

    // Handle switchable options
    const handleSwitchChange = (name: string, checked: boolean) => {
        setFormData(prev => ({...prev, [name]: checked}));
    };

    // Handle select value addition
    const handleAddValue = () => {
        if (!newValue.trim()) return;
        if (selectValues.includes(newValue.trim())) return;

        setSelectValues(prev => [...prev, newValue.trim()]);
        setNewValue('');
    };

    // Handle select value removal
    const handleRemoveValue = (value: string) => {
        setSelectValues(prev => prev.filter(v => v !== value));
    };

    // Handle color value addition
    const handleAddColor = () => {
        if (!newColorName.trim()) return;
        if (colorValues.some(c => c.name === newColorName.trim())) return;

        setColorValues(prev => [...prev, {name: newColorName.trim(), hex: newColorHex}]);
        setNewColorName('');
        setNewColorHex('#000000');
    };

    // Handle color value removal
    const handleRemoveColor = (name: string) => {
        setColorValues(prev => prev.filter(c => c.name !== name));
    };

    // Auto-generate system name from display name
    const handleDisplayNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const displayName = e.target.value;

        // Only auto-generate name if we're creating a new attribute and name hasn't been manually changed
        if (!attributeId) {
            const name = displayName
                .toLowerCase()
                .replace(/[^\w\s-]/g, '')
                .replace(/\s+/g, '_');

            setFormData(prev => ({...prev, displayName, name}));
        } else {
            setFormData(prev => ({...prev, displayName}));
        }
    };

    // Handle unit for number, dimensions, and weight fields
    const handleUnitChange = (value: string) => {
        setFormData(prev => ({
            ...prev,
            configuration: {
                ...prev.configuration,
                unit: value
            }
        }));
    };

    // Handle precision for weight
    const handlePrecisionChange = (value: string) => {
        setFormData(prev => ({
            ...prev,
            configuration: {
                ...prev.configuration,
                precision: parseInt(value)
            }
        }));
    };

    // Handle form submission
    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();

        // Prepare configuration based on attribute type
        let configuration = {...formData.configuration};

        if (formData.type === AttributeType.Select) {
            configuration = {
                ...configuration,
                values: selectValues
            };
        } else if (formData.type === AttributeType.Color) {
            configuration = {
                ...configuration,
                values: colorValues
            };
        }

        const updatedFormData = {
            ...formData,
            configuration
        };

        if (attributeId) {
            // When editing, extract only the fields that should be in the UpdateAttributeRequest
            const updateData: UpdateAttributeRequest = {
                displayName: updatedFormData.displayName,
                configuration: updatedFormData.configuration,
                filterable: updatedFormData.filterable,
                searchable: updatedFormData.searchable,
                isVariant: updatedFormData.isVariant
            };
            await onSubmit(updateData);
        } else {
            // For new attribute, send the full CreateAttributeRequest
            await onSubmit(updatedFormData);
        }
    };

    if (isLoadingAttribute) {
        return (
            <div className="flex justify-center items-center py-8">
                <Loader2 className="h-8 w-8 animate-spin text-primary"/>
                <span className="ml-2">Loading attribute data...</span>
            </div>
        );
    }

    // Render different configuration UI based on attribute type
    const renderConfigurationFields = () => {
        switch (formData.type) {
            case AttributeType.Text:
                return null; // No additional configuration for Text

            case AttributeType.Number:
                return (
                    <div className="space-y-4">
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Unit</label>
                            <Input
                                placeholder="Unit (e.g., cm, kg, etc.)"
                                value={formData.configuration?.unit || ''}
                                onChange={(e) => handleUnitChange(e.target.value)}
                            />
                        </div>
                    </div>
                );

            case AttributeType.Boolean:
                return null; // No additional configuration for Boolean

            case AttributeType.Select:
                return (
                    <div className="space-y-4">
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Options</label>
                            <div className="flex space-x-2">
                                <Input
                                    value={newValue}
                                    onChange={(e) => setNewValue(e.target.value)}
                                    placeholder="Enter option value"
                                    onKeyDown={(e) => {
                                        if (e.key === 'Enter') {
                                            e.preventDefault();
                                            handleAddValue();
                                        }
                                    }}
                                />
                                <Button
                                    type="button"
                                    onClick={handleAddValue}
                                    size="icon"
                                >
                                    <Plus className="size-4"/>
                                </Button>
                            </div>

                            {selectValues.length > 0 ? (
                                <Card className="mt-4">
                                    <CardContent className="p-4 space-y-2">
                                        {selectValues.map((value) => (
                                            <div key={value}
                                                 className="flex items-center justify-between p-2 border rounded-md">
                                                <span>{value}</span>
                                                <Button
                                                    type="button"
                                                    variant="ghost"
                                                    size="icon"
                                                    onClick={() => handleRemoveValue(value)}
                                                >
                                                    <X className="size-4 text-destructive"/>
                                                </Button>
                                            </div>
                                        ))}
                                    </CardContent>
                                </Card>
                            ) : (
                                <p className="text-xs text-muted-foreground italic mt-2">
                                    No options added yet. Add at least one option.
                                </p>
                            )}
                        </div>
                    </div>
                );

            case AttributeType.Color:
                return (
                    <div className="space-y-4">
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Colors</label>
                            <div className="flex flex-col space-y-2">
                                <div className="flex space-x-2">
                                    <Input
                                        value={newColorName}
                                        onChange={(e) => setNewColorName(e.target.value)}
                                        placeholder="Color name (e.g., Ruby Red)"
                                    />
                                    <Input
                                        type="color"
                                        value={newColorHex}
                                        onChange={(e) => setNewColorHex(e.target.value)}
                                        className="w-16"
                                    />
                                    <Button
                                        type="button"
                                        onClick={handleAddColor}
                                        size="icon"
                                    >
                                        <Plus className="size-4"/>
                                    </Button>
                                </div>
                            </div>

                            {colorValues.length > 0 ? (
                                <Card className="mt-4">
                                    <CardContent className="p-4 space-y-2">
                                        {colorValues.map((color) => (
                                            <div key={color.name}
                                                 className="flex items-center justify-between p-2 border rounded-md">
                                                <div className="flex items-center space-x-2">
                                                    <div
                                                        className="w-6 h-6 rounded-full border"
                                                        style={{backgroundColor: color.hex}}
                                                    />
                                                    <span>{color.name}</span>
                                                    <span className="text-xs text-muted-foreground">{color.hex}</span>
                                                </div>
                                                <Button
                                                    type="button"
                                                    variant="ghost"
                                                    size="icon"
                                                    onClick={() => handleRemoveColor(color.name)}
                                                >
                                                    <X className="size-4 text-destructive"/>
                                                </Button>
                                            </div>
                                        ))}
                                    </CardContent>
                                </Card>
                            ) : (
                                <p className="text-xs text-muted-foreground italic mt-2">
                                    No colors added yet. Add at least one color.
                                </p>
                            )}
                        </div>
                    </div>
                );

            case AttributeType.Date:
                return null; // No additional configuration for Date

            case AttributeType.Dimensions:
                return (
                    <div className="space-y-4">
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Unit</label>
                            <Select
                                value={formData.configuration?.unit || 'cm'}
                                onValueChange={handleUnitChange}
                            >
                                <SelectTrigger>
                                    <SelectValue placeholder="Select unit"/>
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="cm">Centimeters (cm)</SelectItem>
                                    <SelectItem value="m">Meters (m)</SelectItem>
                                    <SelectItem value="in">Inches (in)</SelectItem>
                                    <SelectItem value="ft">Feet (ft)</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                    </div>
                );

            case AttributeType.Weight:
                return (
                    <div className="space-y-4">
                        <div className="space-y-2">
                            <label className="text-sm font-medium">Unit</label>
                            <Select
                                value={formData.configuration?.unit || 'kg'}
                                onValueChange={handleUnitChange}
                            >
                                <SelectTrigger>
                                    <SelectValue placeholder="Select unit"/>
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="kg">Kilograms (kg)</SelectItem>
                                    <SelectItem value="g">Grams (g)</SelectItem>
                                    <SelectItem value="lb">Pounds (lb)</SelectItem>
                                    <SelectItem value="oz">Ounces (oz)</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>

                        <div className="space-y-2">
                            <label className="text-sm font-medium">Precision</label>
                            <Select
                                value={formData.configuration?.precision?.toString() || '2'}
                                onValueChange={handlePrecisionChange}
                            >
                                <SelectTrigger>
                                    <SelectValue placeholder="Select precision"/>
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value="0">0 decimal places</SelectItem>
                                    <SelectItem value="1">1 decimal place</SelectItem>
                                    <SelectItem value="2">2 decimal places</SelectItem>
                                    <SelectItem value="3">3 decimal places</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>
                    </div>
                );

            default:
                return null;
        }
    };

    return (
        <form onSubmit={handleSubmit} className="space-y-4">
            {/* Display Name field */}
            <div className="space-y-2">
                <label className="text-sm font-medium" htmlFor="displayName">
                    Display Name <span className="text-destructive">*</span>
                </label>
                <Input
                    id="displayName"
                    name="displayName"
                    value={formData.displayName}
                    onChange={handleDisplayNameChange}
                    placeholder="Size, Color, Material, etc."
                    required
                />
                <p className="text-xs text-muted-foreground">
                    The name shown to users in the UI.
                </p>
            </div>

            {/* System Name field - Only show when creating a new attribute */}
            {!attributeId && (
                <div className="space-y-2">
                    <label className="text-sm font-medium" htmlFor="name">
                        System Name <span className="text-destructive">*</span>
                    </label>
                    <Input
                        id="name"
                        name="name"
                        value={formData.name}
                        onChange={handleChange}
                        placeholder="size, color, material, etc."
                        required
                    />
                    <p className="text-xs text-muted-foreground">
                        Internal name used in the system. Cannot be changed after creation.
                    </p>
                </div>
            )}

            {/* Type field - Only show when creating a new attribute */}
            {!attributeId && (
                <div className="space-y-2">
                    <label className="text-sm font-medium">
                        Type <span className="text-destructive">*</span>
                    </label>
                    <Select
                        value={formData.type}
                        onValueChange={handleTypeChange}
                    >
                        <SelectTrigger>
                            <SelectValue placeholder="Select attribute type"/>
                        </SelectTrigger>
                        <SelectContent>
                            <SelectItem value={AttributeType.Text}>Text</SelectItem>
                            <SelectItem value={AttributeType.Number}>Number</SelectItem>
                            <SelectItem value={AttributeType.Boolean}>Boolean</SelectItem>
                            <SelectItem value={AttributeType.Select}>Select</SelectItem>
                            <SelectItem value={AttributeType.Color}>Color</SelectItem>
                            <SelectItem value={AttributeType.Date}>Date</SelectItem>
                            <SelectItem value={AttributeType.Dimensions}>Dimensions</SelectItem>
                            <SelectItem value={AttributeType.Weight}>Weight</SelectItem>
                        </SelectContent>
                    </Select>
                    <p className="text-xs text-muted-foreground">
                        Determines how the attribute is displayed and used. Cannot be changed after creation.
                    </p>
                </div>
            )}

            {/* If editing, display the type as read-only text */}
            {attributeId && (
                <div className="space-y-2">
                    <label className="text-sm font-medium">Type</label>
                    <div className="p-2 border rounded-md bg-muted/50">
                        {formData.type}
                    </div>
                    <p className="text-xs text-muted-foreground">
                        Attribute type cannot be changed after creation.
                    </p>
                </div>
            )}

            {/* Type-specific configuration fields */}
            {renderConfigurationFields()}

            {/* Variant switch */}
            <div className="flex items-center space-x-2">
                <Switch
                    id="isVariant"
                    checked={formData.isVariant}
                    onCheckedChange={(checked) => handleSwitchChange('isVariant', checked)}
                />
                <label htmlFor="isVariant" className="text-sm font-medium cursor-pointer">
                    Use for variants
                </label>
                <p className="text-xs text-muted-foreground ml-2">
                    Attributes used for variants create different product options (e.g., different sizes, colors).
                </p>
            </div>

            {/* Filterable switch */}
            <div className="flex items-center space-x-2">
                <Switch
                    id="filterable"
                    checked={formData.filterable}
                    onCheckedChange={(checked) => handleSwitchChange('filterable', checked)}
                />
                <label htmlFor="filterable" className="text-sm font-medium cursor-pointer">
                    Filterable
                </label>
                <p className="text-xs text-muted-foreground ml-2">
                    Allow customers to filter products by this attribute.
                </p>
            </div>

            {/* Searchable switch */}
            <div className="flex items-center space-x-2">
                <Switch
                    id="searchable"
                    checked={formData.searchable}
                    onCheckedChange={(checked) => handleSwitchChange('searchable', checked)}
                />
                <label htmlFor="searchable" className="text-sm font-medium cursor-pointer">
                    Searchable
                </label>
                <p className="text-xs text-muted-foreground ml-2">
                    Include this attribute in product search.
                </p>
            </div>

            {/* Form actions */}
            <div className="pt-4 flex justify-end space-x-2">
                <Button type="button" variant="outline" onClick={onCancel} disabled={isLoading}>
                    Cancel
                </Button>
                <Button
                    type="submit"
                    disabled={
                        isLoading ||
                        (formData.type === AttributeType.Select && selectValues.length === 0) ||
                        (formData.type === AttributeType.Color && colorValues.length === 0)
                    }
                >
                    {isLoading && <Loader2 className="size-4 mr-2 animate-spin"/>}
                    {attributeId ? 'Update' : 'Create'} Attribute
                </Button>
            </div>
        </form>
    );
};

export default AttributeForm;