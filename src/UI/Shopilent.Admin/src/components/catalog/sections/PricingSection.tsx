// src/pages/catalog/sections/PricingSection.tsx (corrected)
import React from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { FormField } from '@/components/ui/form-field';
import { Input } from '@/components/ui/input';
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from '@/components/ui/select';

interface PricingSectionProps {
    basePrice: number;
    currency: string;
    errors: Record<string, string>;
    onNumberChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
    onCurrencyChange: (value: string) => void;
}

const PricingSection: React.FC<PricingSectionProps> = ({
                                                           basePrice,
                                                           currency,
                                                           errors,
                                                           onNumberChange,
                                                           onCurrencyChange
                                                       }) => {
    return (
        <Card>
            <CardHeader>
                <CardTitle>Product Pricing</CardTitle>
                <CardDescription>
                    Set the price and currency for your product.
                </CardDescription>
            </CardHeader>
            <CardContent>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    {/* Base Price */}
                    <FormField
                        label="Base Price"
                        error={errors.basePrice}
                        required
                        htmlFor="basePrice"
                    >
                        <Input
                            id="basePrice"
                            name="basePrice"
                            type="number"
                            step="0.01"
                            min="0"
                            value={basePrice}
                            onChange={onNumberChange}
                            className={errors.basePrice ? "border-destructive" : ""}
                            required
                        />
                    </FormField>

                    {/* Currency */}
                    <FormField
                        label="Currency"
                        error={errors.currency}
                        required
                        htmlFor="currency"
                    >
                        <Select
                            value={currency}
                            onValueChange={onCurrencyChange}
                        >
                            <SelectTrigger id="currency" className={errors.currency ? "border-destructive" : ""}>
                                <SelectValue placeholder="Select currency" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value="USD">US Dollar (USD)</SelectItem>
                                <SelectItem value="EUR">Euro (EUR)</SelectItem>
                                <SelectItem value="GBP">British Pound (GBP)</SelectItem>
                                <SelectItem value="CAD">Canadian Dollar (CAD)</SelectItem>
                                <SelectItem value="AUD">Australian Dollar (AUD)</SelectItem>
                                <SelectItem value="JPY">Japanese Yen (JPY)</SelectItem>
                            </SelectContent>
                        </Select>
                    </FormField>
                </div>
            </CardContent>
        </Card>
    );
};

export default PricingSection;