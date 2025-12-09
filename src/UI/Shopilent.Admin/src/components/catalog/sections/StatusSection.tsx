// src/pages/catalog/sections/StatusSection.tsx
import React from 'react';
import { Loader2, Save } from 'lucide-react';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';

interface StatusSectionProps {
    isActive: boolean;
    isEditMode: boolean;
    isSubmitting: boolean;
    isPending: boolean;
    onSwitchChange: (checked: boolean) => void;
}

const StatusSection: React.FC<StatusSectionProps> = ({
                                                         isActive,
                                                         isEditMode,
                                                         isSubmitting,
                                                         isPending,
                                                         onSwitchChange
                                                     }) => {
    return (
        <Card>
            <CardHeader>
                <CardTitle>Product Status</CardTitle>
                <CardDescription>
                    Control the visibility of this product.
                </CardDescription>
            </CardHeader>
            <CardContent>
                <div className="flex items-center space-x-2">
                    <Switch
                        id="isActive"
                        checked={isActive}
                        onCheckedChange={onSwitchChange}
                    />
                    <label htmlFor="isActive" className="text-sm font-medium cursor-pointer">
                        {isActive ? 'Active' : 'Inactive'}
                    </label>
                </div>
                <p className="text-xs text-muted-foreground mt-2">
                    {isActive
                        ? 'Product is visible to customers.'
                        : 'Product is hidden from customers.'}
                </p>
            </CardContent>
            <CardFooter>
                <Button
                    type="submit"
                    className="w-full"
                    disabled={isSubmitting || isPending}
                >
                    {(isSubmitting || isPending) ? (
                        <>
                            <Loader2 className="size-4 mr-2 animate-spin" />
                            {isEditMode ? 'Updating...' : 'Creating...'}
                        </>
                    ) : (
                        <>
                            <Save className="size-4 mr-2" />
                            {isEditMode ? 'Update Product' : 'Create Product'}
                        </>
                    )}
                </Button>
            </CardFooter>
        </Card>
    );
};

export default StatusSection;