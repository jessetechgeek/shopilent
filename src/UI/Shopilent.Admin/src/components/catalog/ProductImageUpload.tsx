// src/components/catalog/ProductImageUpload.tsx
import React, { useState } from 'react';
import { Upload, X, Image as ImageIcon, Loader2, ZoomIn, GripVertical } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
} from '@/components/ui/dialog';

interface ProductImageUploadProps {
    images: Array<{
        url: string,
        file?: File,
        imageKey?: string,
        isDefault?: boolean,
        displayOrder?: number
    }>;
    onImagesChange: (images: Array<{
        url: string,
        file?: File,
        imageKey?: string,
        isDefault?: boolean,
        displayOrder?: number
    }>) => void;
    onImageRemove?: (imageKey: string) => void;
    isLoading?: boolean;
}

const ProductImageUpload: React.FC<ProductImageUploadProps> = ({
                                                                   images,
                                                                   onImagesChange,
                                                                   onImageRemove,
                                                                   isLoading = false
                                                               }) => {
    const [uploadError, setUploadError] = useState<string | null>(null);
    const [selectedImageIndex, setSelectedImageIndex] = useState<number | null>(null);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
    const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

    const handleImageUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
        setUploadError(null);
        const files = e.target.files;

        if (!files || files.length === 0) return;

        const newImages = [...images];

        Array.from(files).forEach(file => {
            // Check file type
            if (!file.type.startsWith('image/')) {
                setUploadError('Only image files are allowed');
                return;
            }

            // Check file size (limit to 5MB)
            if (file.size > 5 * 1024 * 1024) {
                setUploadError('Image size must be less than 5MB');
                return;
            }

            // Create a preview URL for display purposes
            const imageUrl = URL.createObjectURL(file);

            // Store both the URL for preview and the actual File object
            newImages.push({
                url: imageUrl,
                file: file
            });
        });

        onImagesChange(newImages);

        // Reset input
        e.target.value = '';
    };

    const handleRemoveImage = (index: number) => {
        const newImages = [...images];
        const removedImage = newImages[index];

        // If the image has a preview URL created with createObjectURL, revoke it
        if (removedImage.url.startsWith('blob:')) {
            URL.revokeObjectURL(removedImage.url);
        }

        // If it's a server image and has an imageKey, track it for removal
        if (removedImage.imageKey && onImageRemove) {
            onImageRemove(removedImage.imageKey);
        }

        newImages.splice(index, 1);
        onImagesChange(newImages);
    };

    const handleDragStart = (e: React.DragEvent<HTMLDivElement>, index: number) => {
        setDraggedIndex(index);
        e.dataTransfer.effectAllowed = 'move';
        e.dataTransfer.setData('text/html', '');

        // Add a slight delay to prevent the drag preview from being the drag target
        setTimeout(() => {
            const draggedElement = e.currentTarget;
            draggedElement.style.opacity = '0.5';
        }, 0);
    };

    const handleDragEnd = (e: React.DragEvent<HTMLDivElement>) => {
        const draggedElement = e.currentTarget;
        draggedElement.style.opacity = '1';
        setDraggedIndex(null);
        setDragOverIndex(null);
    };

    const handleDragOver = (e: React.DragEvent<HTMLDivElement>, index: number) => {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';

        if (draggedIndex !== null && draggedIndex !== index) {
            setDragOverIndex(index);
        }
    };

    const handleDragLeave = (e: React.DragEvent<HTMLDivElement>) => {
        // Only clear drag over if we're leaving the entire card area
        const rect = e.currentTarget.getBoundingClientRect();
        const x = e.clientX;
        const y = e.clientY;

        if (x < rect.left || x > rect.right || y < rect.top || y > rect.bottom) {
            setDragOverIndex(null);
        }
    };

    const handleDrop = (e: React.DragEvent<HTMLDivElement>, dropIndex: number) => {
        e.preventDefault();

        if (draggedIndex === null || draggedIndex === dropIndex) {
            setDragOverIndex(null);
            return;
        }

        const newImages = [...images];
        const draggedImage = newImages[draggedIndex];

        // Remove the dragged image from its original position
        newImages.splice(draggedIndex, 1);

        // Insert the dragged image at the new position
        const actualDropIndex = draggedIndex < dropIndex ? dropIndex - 1 : dropIndex;
        newImages.splice(actualDropIndex, 0, draggedImage);

        onImagesChange(newImages);
        setDragOverIndex(null);
    };

    const handleImageClick = (index: number) => {
        setSelectedImageIndex(index);
        setIsModalOpen(true);
    };

    const handleNavigateImage = (direction: 'prev' | 'next') => {
        if (selectedImageIndex === null) return;

        if (direction === 'prev' && selectedImageIndex > 0) {
            setSelectedImageIndex(selectedImageIndex - 1);
        } else if (direction === 'next' && selectedImageIndex < images.length - 1) {
            setSelectedImageIndex(selectedImageIndex + 1);
        }
    };

    const getImageSrc = (image: { url: string, file?: File, imageKey?: string }) => {
        // For new files that have been uploaded but not saved to server
        if (image.file && image.url.startsWith('blob:')) {
            return image.url;
        }
        // For existing server images
        return image.url;
    };

    return (
        <div className="space-y-4">
            {uploadError && (
                <div className="bg-destructive/10 text-destructive p-3 rounded-md text-sm">
                    {uploadError}
                </div>
            )}

            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
                {images.map((image, index) => (
                    <div
                        key={index}
                        draggable={!isLoading}
                        onDragStart={(e) => handleDragStart(e, index)}
                        onDragEnd={handleDragEnd}
                        onDragOver={(e) => handleDragOver(e, index)}
                        onDragLeave={handleDragLeave}
                        onDrop={(e) => handleDrop(e, index)}
                        className={`relative group cursor-move transition-all duration-200 ${
                            dragOverIndex === index
                                ? 'scale-105 ring-2 ring-primary ring-offset-2'
                                : ''
                        } ${
                            draggedIndex === index
                                ? 'scale-95 rotate-2'
                                : ''
                        }`}
                    >
                        <Card className="aspect-square overflow-hidden border-2 border-transparent hover:border-primary/20 transition-colors">
                            {/* Order Badge - Top Left */}
                            <div className="absolute top-2 left-2 z-20">
                                <div className="bg-primary text-primary-foreground text-xs rounded-full size-6 flex items-center justify-center font-medium">
                                    {index + 1}
                                </div>
                            </div>

                            {/* Drag Handle */}
                            <div className="absolute top-2 left-10 z-20 opacity-0 group-hover:opacity-100 transition-opacity">
                                <div className="bg-black/50 rounded p-1">
                                    <GripVertical className="size-3 text-white" />
                                </div>
                            </div>

                            {/* Remove Button - Top Right */}
                            <Button
                                type="button"
                                variant="destructive"
                                size="icon"
                                className="absolute top-2 right-2 size-6 z-30"
                                onClick={(e) => {
                                    e.stopPropagation();
                                    handleRemoveImage(index);
                                }}
                                disabled={isLoading}
                            >
                                <X className="size-3" />
                            </Button>

                            {/* Zoom Button */}
                            <Button
                                type="button"
                                variant="secondary"
                                size="icon"
                                className="absolute bottom-2 right-2 size-6 z-20 opacity-0 group-hover:opacity-100 transition-opacity"
                                onClick={(e) => {
                                    e.stopPropagation();
                                    handleImageClick(index);
                                }}
                            >
                                <ZoomIn className="size-3" />
                            </Button>

                            <CardContent className="p-0 h-full">
                                <img
                                    src={getImageSrc(image)}
                                    alt={`Product image ${index + 1}`}
                                    className="w-full h-full object-cover cursor-pointer"
                                    onClick={() => handleImageClick(index)}
                                    draggable={false}
                                />
                            </CardContent>
                        </Card>

                        {/* First Image Badge */}
                        {index === 0 && (
                            <div className="absolute -bottom-2 left-1/2 transform -translate-x-1/2 z-10">
                                <div className="bg-primary text-primary-foreground text-xs px-2 py-1 rounded-full whitespace-nowrap">
                                    Thumbnail
                                </div>
                            </div>
                        )}
                    </div>
                ))}

                {/* Upload Button */}
                <label
                    htmlFor="image-upload"
                    className={`cursor-pointer ${isLoading ? 'cursor-not-allowed' : ''}`}
                >
                    <Card className="aspect-square border-2 border-dashed border-muted-foreground/25 hover:border-primary/50 hover:bg-muted/50 transition-colors">
                        <CardContent className="h-full flex flex-col items-center justify-center text-muted-foreground hover:text-primary transition-colors">
                            {isLoading ? (
                                <Loader2 className="size-8 animate-spin" />
                            ) : (
                                <>
                                    <Upload className="size-8" />
                                    <span className="text-xs mt-1 text-center">Upload Images</span>
                                </>
                            )}
                        </CardContent>
                    </Card>
                    <input
                        id="image-upload"
                        type="file"
                        accept="image/*"
                        multiple
                        className="hidden"
                        onChange={handleImageUpload}
                        disabled={isLoading}
                    />
                </label>
            </div>

            {images.length === 0 && (
                <div className="text-center p-8 border border-dashed rounded-md">
                    <ImageIcon className="size-10 mx-auto text-muted-foreground" />
                    <p className="mt-2 text-sm text-muted-foreground">
                        No product images have been uploaded yet
                    </p>
                    <p className="text-xs text-muted-foreground mt-1">
                        Upload product images to showcase your product. Drag and drop to reorder.
                    </p>
                </div>
            )}

            {/* Image Modal */}
            <Dialog open={isModalOpen} onOpenChange={setIsModalOpen}>
                <DialogContent className="max-w-4xl w-[95vw] h-[85vh] p-0 overflow-hidden">
                    <DialogHeader className="p-6 pb-2 border-b">
                        <DialogTitle className="text-lg">
                            Product Image {selectedImageIndex !== null ? selectedImageIndex + 1 : ''} of {images.length}
                        </DialogTitle>
                    </DialogHeader>

                    <div className="flex-1 relative p-6 pt-2">
                        {selectedImageIndex !== null && images[selectedImageIndex] && (
                            <>
                                <div className="relative h-full flex items-center justify-center bg-muted/30 rounded-lg min-h-[500px]">
                                    <img
                                        src={getImageSrc(images[selectedImageIndex])}
                                        alt={`Product image ${selectedImageIndex + 1}`}
                                        className="max-w-full max-h-full object-contain rounded-lg"
                                        style={{ maxHeight: '70vh' }}
                                    />
                                </div>

                                {/* Navigation buttons */}
                                {images.length > 1 && (
                                    <>
                                        <Button
                                            variant="outline"
                                            size="icon"
                                            className="absolute left-2 top-1/2 transform -translate-y-1/2 bg-background/80 backdrop-blur-sm"
                                            onClick={() => handleNavigateImage('prev')}
                                            disabled={selectedImageIndex === 0}
                                        >
                                            ←
                                        </Button>
                                        <Button
                                            variant="outline"
                                            size="icon"
                                            className="absolute right-2 top-1/2 transform -translate-y-1/2 bg-background/80 backdrop-blur-sm"
                                            onClick={() => handleNavigateImage('next')}
                                            disabled={selectedImageIndex === images.length - 1}
                                        >
                                            →
                                        </Button>
                                    </>
                                )}

                                {/* Thumbnail navigation */}
                                {images.length > 1 && (
                                    <div className="absolute bottom-4 left-1/2 transform -translate-x-1/2 flex gap-2 bg-background/80 backdrop-blur-sm p-2 rounded-lg max-w-full overflow-x-auto">
                                        {images.map((image, index) => (
                                            <button
                                                key={index}
                                                onClick={() => setSelectedImageIndex(index)}
                                                className={`flex-shrink-0 w-12 h-12 rounded border-2 overflow-hidden transition-all ${
                                                    selectedImageIndex === index
                                                        ? 'border-primary scale-110'
                                                        : 'border-transparent hover:border-primary/50'
                                                }`}
                                            >
                                                <img
                                                    src={getImageSrc(image)}
                                                    alt={`Thumbnail ${index + 1}`}
                                                    className="w-full h-full object-cover"
                                                />
                                            </button>
                                        ))}
                                    </div>
                                )}
                            </>
                        )}
                    </div>
                </DialogContent>
            </Dialog>
        </div>
    );
};

export default ProductImageUpload;