// src/pages/catalog/sections/ImagesSection.tsx
import React from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import ProductImageUpload from '@/components/catalog/ProductImageUpload';

interface ImagesSectionProps {
    images: Array<{
        url: string;
        file?: File;
        imageKey?: string;
        isDefault?: boolean;
        displayOrder?: number;
    }>;
    onImagesChange: (images: Array<{
        url: string;
        file?: File;
        imageKey?: string;
        isDefault?: boolean;
        displayOrder?: number;
    }>) => void;
    onImageRemove: (imageKey: string) => void;
    isLoading: boolean;
}

const ImagesSection: React.FC<ImagesSectionProps> = ({
                                                         images,
                                                         onImagesChange,
                                                         onImageRemove,
                                                         isLoading
                                                     }) => {
    return (
        <Card>
            <CardHeader>
                <CardTitle>Product Images</CardTitle>
                <CardDescription>
                    Upload and manage product images. The first image will be used as the product thumbnail.
                </CardDescription>
            </CardHeader>
            <CardContent>
                <ProductImageUpload
                    images={images}
                    onImagesChange={onImagesChange}
                    onImageRemove={onImageRemove}
                    isLoading={isLoading}
                />
            </CardContent>
        </Card>
    );
};

export default ImagesSection;