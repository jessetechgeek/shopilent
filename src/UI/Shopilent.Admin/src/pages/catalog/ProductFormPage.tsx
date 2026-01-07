import React, {useState, useEffect} from 'react';
import {useNavigate, useParams} from 'react-router-dom';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {ArrowLeft} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {toast} from '@/components/ui/use-toast';
import {Loading} from '@/components/ui/loading';
import {useForm} from '@/hooks/useForm';
import {useApiMutation} from '@/hooks/useApiMutation';
import {useTitle} from '@/hooks/useTitle';

// Import section components
import BasicDetailsSection from '@/components/catalog/sections/BasicDetailsSection';
import PricingSection from '@/components/catalog/sections/PricingSection';
import StatusSection from '@/components/catalog/sections/StatusSection';
import ImagesSection from '@/components/catalog/sections/ImagesSection';
import CategoriesSection from '@/components/catalog/sections/CategoriesSection';

// Import components
import ProductAttributeSelector from '@/components/catalog/ProductAttributeSelector';
import VariantList from '@/components/catalog/VariantList';

// Import API functions
import {productApi} from '@/api/products';
import {categoryApi} from '@/api/categories';
import {attributeApi} from '@/api/attributes';
import {variantApi} from '@/api/variants';

// Import types
import {
  CreateProductRequest,
  UpdateProductRequest,
  CategoryDto,
  ProductAttributeDto,
  ProductVariantDto,
  ProductImageDto,
  CreateProductVariantRequest,
  UpdateProductVariantRequest,
  UpdateVariantStatusRequest,
  UpdateVariantStockRequest
} from '@/models/catalog';

const ProductFormPage: React.FC = () => {
  const {id} = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isEditMode = !!id;

  useTitle(isEditMode ? 'Edit Product' : 'Add Product');

  // State for tracking removed images
  const [imagesToRemove, setImagesToRemove] = useState<string[]>([]);

  // State for product images
  const [productImages, setProductImages] = useState<Array<{
    url: string;
    file?: File;
    imageKey?: string;
    isDefault?: boolean;
    displayOrder?: number;
  }>>([]);

  // State for selected categories
  const [selectedCategories, setSelectedCategories] = useState<string[]>([]);

  // State for product attributes
  const [productAttributes, setProductAttributes] = useState<Array<{
    attributeId: string;
    value: any;
  }>>([]);

  // State for variants
  const [productVariants, setProductVariants] = useState<ProductVariantDto[]>([]);
  // Remove unused variantError state
  // const [variantError, setVariantError] = useState<string | null>(null);

  // Form state management using custom hook
  const {
    values: formData,
    errors,
    isSubmitting,
    handleChange,
    handleNumberChange,
    handleSwitchChange,
    handleSelectChange,
    setValue,
    setValues,
    setErrors,
    handleSubmit
  } = useForm({
    initialValues: {
      name: '',
      description: '',
      basePrice: 0,
      currency: 'USD',
      sku: '',
      slug: '',
      isActive: true,
      metadata: {}
    },
    onSubmit: async (values) => {
      // Form validation
      if (!validateForm()) {
        toast({
          title: 'Validation Error',
          description: 'Please fill in all required fields',
          variant: 'destructive'
        });
        return;
      }

      try {
        // Prepare the request data
        const requestData: CreateProductRequest | UpdateProductRequest = {
          name: values.name,
          description: values.description,
          basePrice: values.basePrice,
          currency: values.currency,
          sku: values.sku,
          slug: values.slug,
          isActive: values.isActive,
          categoryIds: selectedCategories,
          attributes: productAttributes,
          metadata: values.metadata
        };

        // Prepare files from images
        const files = productImages
          .filter(img => img.file)
          .map(img => img.file as File);

        if (isEditMode && id) {
          // For updates, ALWAYS include imageOrders
          const updateData: UpdateProductRequest = {
            ...requestData,
            ...(imagesToRemove.length > 0 && {imagesToRemove: imagesToRemove}),
            // ALWAYS include imageOrders for updates
            imageOrders: productImages
              .filter(img => img.imageKey && !imagesToRemove.includes(img.imageKey))
              .map((img, index) => ({
                imageKey: img.imageKey!,
                displayOrder: index,
                isDefault: index === 0
              }))
          };

          console.log('🚀 Update request with imageOrders:', updateData);

          await updateProductMutation.mutateAsync({
            id,
            data: updateData,
            files: files.length > 0 ? files : undefined
          });
        } else {
          // Create new product (no imageOrders needed)
          await createProductMutation.mutateAsync({
            ...requestData,
            files: files.length > 0 ? files : undefined
          } as CreateProductRequest & { files?: File[] });
        }
      } catch (error) {
        console.error('Form submission error:', error);
      }
    },
    validate: (values) => {
      const errors: Record<string, string> = {};

      if (!values.name.trim()) {
        errors.name = 'Product name is required';
      }

      if (!values.slug.trim()) {
        errors.slug = 'Slug is required';
      }

      if (values.basePrice <= 0) {
        errors.basePrice = 'Base price must be greater than zero';
      }

      if (!values.currency) {
        errors.currency = 'Currency is required';
      }

      if (selectedCategories.length === 0) {
        errors.categories = 'At least one category must be selected';
      }

      return errors;
    }
  });

  // ==========================================
  // Queries
  // ==========================================

  // Query for product (in edit mode)
  const {data: product, isLoading: isLoadingProduct} = useQuery({
    queryKey: ['product', id],
    queryFn: async () => {
      if (!id) return null;
      const response = await productApi.getProductById(id);
      if (response.data.succeeded) {
        return response.data.data;
      }
      throw new Error(response.data.message || 'Failed to fetch product');
    },
    enabled: isEditMode
  });

  // Query for all categories
  const {data: categories, isLoading: isLoadingCategories} = useQuery({
    queryKey: ['categories-all'],
    queryFn: async () => {
      const response = await categoryApi.getCategories();
      if (response.data.succeeded) {
        return response.data.data;
      }
      throw new Error(response.data.message || 'Failed to fetch categories');
    }
  });

  // Query for attributes
  const {data: attributes, isLoading: isLoadingAttributes} = useQuery({
    queryKey: ['attributes-all'],
    queryFn: async () => {
      const response = await attributeApi.getAttributes();
      if (response.data.succeeded) {
        return response.data.data;
      }
      throw new Error(response.data.message || 'Failed to fetch attributes');
    }
  });

  // ==========================================
  // API Mutations
  // ==========================================

  // Create product mutation
  const createProductMutation = useApiMutation({
    mutationFn: (data: CreateProductRequest & { files?: File[] }) => {
      const {files, ...rest} = data;
      return productApi.createProduct(rest, files);
    },
    onSuccess: (response) => {
      const productId = response.data.data.id;
      toast({
        title: 'Success',
        description: 'Product created successfully',
        variant: 'success'
      });
      queryClient.invalidateQueries({queryKey: ['products']});
      navigate(`/catalog/products/edit/${productId}`);
    }
  });

  // Update product mutation
  const updateProductMutation = useApiMutation({
    mutationFn: ({id, data, files}: {
      id: string;
      data: UpdateProductRequest;
      files?: File[]
    }) => productApi.updateProduct(id, data, files),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Product updated successfully',
        variant: 'success'
      });
      queryClient.invalidateQueries({queryKey: ['products']});
      queryClient.invalidateQueries({queryKey: ['product', id]});
    }
  });

  // Variant mutations
  const createVariantMutation = useApiMutation({
    mutationFn: (data: CreateProductVariantRequest) => variantApi.createVariant(data),
    onSuccess: (response) => {
      setProductVariants(prev => [...prev, response.data.data]);
      queryClient.invalidateQueries({queryKey: ['product', id]});
    }
  });

  const updateVariantMutation = useApiMutation({
    mutationFn: ({id, data}: { id: string; data: UpdateProductVariantRequest }) =>
      variantApi.updateVariant(id, data),
    onSuccess: (response) => {
      setProductVariants(prev =>
        prev.map(v => v.id === response.data.data.id ? response.data.data : v)
      );
    }
  });

  const deleteVariantMutation = useApiMutation({
    mutationFn: (id: string) => variantApi.deleteVariant(id),
    onSuccess: (_, variantId) => {
      setProductVariants(prev => prev.filter(v => v.id !== variantId));
      queryClient.invalidateQueries({queryKey: ['product', id]});
    }
  });

  const updateVariantStatusMutation = useApiMutation({
    mutationFn: ({id, data}: { id: string; data: UpdateVariantStatusRequest }) =>
      variantApi.updateVariantStatus(id, data),
    onSuccess: (response) => {
      setProductVariants(prev =>
        prev.map(v => v.id === response.data.data.id ? response.data.data : v)
      );
      queryClient.invalidateQueries({queryKey: ['product', id]});
    }
  });

  const updateVariantStockMutation = useApiMutation({
    mutationFn: ({id, data}: { id: string; data: UpdateVariantStockRequest }) =>
      variantApi.updateVariantStock(id, data),
    onSuccess: (response) => {
      setProductVariants(prev =>
        prev.map(v => v.id === response.data.data.id ? response.data.data : v)
      );
      queryClient.invalidateQueries({queryKey: ['product', id]});
    }
  });

  // ==========================================
  // Effect Hooks
  // ==========================================

  // Load existing product data when in edit mode
  useEffect(() => {
    if (product) {
      setValues({
        name: product.name,
        description: product.description || '',
        basePrice: product.basePrice,
        currency: product.currency,
        sku: product.sku || '',
        slug: product.slug,
        isActive: product.isActive,
        metadata: product.metadata || {}
      });

      // Extract category IDs
      if (product.categories) {
        const categoryIds = product.categories.map((cat: CategoryDto) => cat.id);
        setSelectedCategories(categoryIds);
      }

      // Extract attributes
      if (product.attributes) {
        const attrs = product.attributes.map((attr: ProductAttributeDto) => ({
          attributeId: attr.attributeId,
          value: attr.values.value
        }));
        setProductAttributes(attrs);
      }

      // Extract images - use imageUrl from API response instead of constructing from imageKey
      if (product.images && Array.isArray(product.images)) {
        const loadedImages = product.images.map((img: ProductImageDto) => ({
          url: img.imageUrl || '',
          imageKey: img.imageKey
        }));
        setProductImages(loadedImages);
      }

      // Set variants
      if (product.variants) {
        setProductVariants(product.variants);
      }
    }
  }, [product, setValues]);

  // ==========================================
  // Event Handlers
  // ==========================================

  // Auto-generate slug from name
  const handleNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const name = e.target.value;
    const slug = name
      .toLowerCase()
      .replace(/[^\w\s-]/g, '')
      .replace(/\s+/g, '-');

    setValue('name', name);
    setValue('slug', slug);
  };

  // Handle category selection
  const handleCategoryChange = (categoryId: string) => {
    const isSelected = selectedCategories.includes(categoryId);
    let updatedCategories: string[];

    if (isSelected) {
      updatedCategories = selectedCategories.filter(id => id !== categoryId);
    } else {
      updatedCategories = [...selectedCategories, categoryId];
    }

    setSelectedCategories(updatedCategories);

    // Clear error if at least one category is selected
    if (updatedCategories.length > 0) {
      setErrors(prev => ({...prev, categories: ''}));
    }
  };

  // Handle product attributes change
  const handleAttributesChange = (attributes: Array<{ attributeId: string; value: any }>) => {
    setProductAttributes(attributes);
  };

  // Handle image removal tracking
  const handleImageRemove = (imageKey: string) => {
    setImagesToRemove(prev => [...prev, imageKey]);
  };

  // Variant handlers - Fixed to return Promise<void>
  const handleAddVariant = async (data: CreateProductVariantRequest): Promise<void> => {
    await createVariantMutation.mutateAsync(data);
  };

  const handleUpdateVariant = async (id: string, data: UpdateProductVariantRequest): Promise<void> => {
    await updateVariantMutation.mutateAsync({id, data});
  };

  const handleDeleteVariant = async (id: string): Promise<void> => {
    await deleteVariantMutation.mutateAsync(id);
  };

  const handleUpdateVariantStatus = async (id: string, data: UpdateVariantStatusRequest): Promise<void> => {
    await updateVariantStatusMutation.mutateAsync({id, data});
  };

  const handleUpdateVariantStock = async (id: string, data: UpdateVariantStockRequest): Promise<void> => {
    await updateVariantStockMutation.mutateAsync({id, data});
  };

  // Validate form before submission
  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};

    // Required fields validation
    if (!formData.name.trim()) {
      newErrors.name = 'Product name is required';
    }

    if (!formData.slug.trim()) {
      newErrors.slug = 'Slug is required';
    }

    if (formData.basePrice <= 0) {
      newErrors.basePrice = 'Base price must be greater than zero';
    }

    if (!formData.currency) {
      newErrors.currency = 'Currency is required';
    }

    // Check if at least one category is selected
    if (!selectedCategories.length) {
      newErrors.categories = 'At least one category must be selected';
    }

    setErrors(newErrors); // Make sure setErrors is properly defined
    return Object.keys(newErrors).length === 0;
  };

  // Get currency symbol for price formatting
  const getCurrencySymbol = (currency: string): string => {
    switch (currency) {
      case 'USD':
        return '$';
      case 'EUR':
        return '€';
      case 'GBP':
        return '£';
      case 'CAD':
        return 'CA$';
      case 'AUD':
        return 'A$';
      case 'JPY':
        return '¥';
      default:
        return '$';
    }
  };

  // Loading state
  if (isEditMode && isLoadingProduct) {
    return <Loading text="Loading product data..."/>;
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">
            {isEditMode ? 'Edit Product' : 'Create New Product'}
          </h1>
          <p className="text-muted-foreground">
            {isEditMode
              ? 'Update your product information'
              : 'Fill in the details to create a new product'}
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => navigate('/catalog/products')}
        >
          <ArrowLeft className="size-4 mr-2"/>
          Back to Products
        </Button>
      </div>

      <form onSubmit={handleSubmit} className="space-y-8">
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Left Column */}
          <div className="lg:col-span-2 space-y-6">
            {/* Basic Details */}
            <BasicDetailsSection
              name={formData.name}
              slug={formData.slug}
              description={formData.description}
              sku={formData.sku}
              errors={errors}
              onNameChange={handleNameChange}
              onInputChange={handleChange}
            />

            {/* Pricing Section */}
            <PricingSection
              basePrice={formData.basePrice}
              currency={formData.currency}
              errors={errors}
              onNumberChange={handleNumberChange}
              onCurrencyChange={(value) => handleSelectChange('currency', value)}
            />

            {/* Images Section */}
            <ImagesSection
              images={productImages}
              onImagesChange={setProductImages}
              onImageRemove={handleImageRemove}
              isLoading={isSubmitting || createProductMutation.isPending || updateProductMutation.isPending}
            />

            {/* Variants Section - Only show in edit mode */}
            {isEditMode && id && (
              <VariantList
                productId={id}
                variants={productVariants}
                attributes={attributes || []}
                productAttributes={productAttributes}
                currencySymbol={getCurrencySymbol(formData.currency)}
                onAddVariant={handleAddVariant}
                onUpdateVariant={handleUpdateVariant}
                onDeleteVariant={handleDeleteVariant}
                onUpdateStatus={handleUpdateVariantStatus}
                onUpdateStock={handleUpdateVariantStock}
                isLoading={
                  createVariantMutation.isPending ||
                  updateVariantMutation.isPending ||
                  deleteVariantMutation.isPending ||
                  updateVariantStatusMutation.isPending ||
                  updateVariantStockMutation.isPending
                }
                basePrice={formData.basePrice}
                baseSku={formData.sku}
              />
            )}
          </div>

          {/* Right Column */}
          <div className="space-y-6">
            {/* Status Card */}
            <StatusSection
              isActive={formData.isActive}
              isEditMode={isEditMode}
              isSubmitting={isSubmitting}
              isPending={createProductMutation.isPending || updateProductMutation.isPending}
              onSwitchChange={(checked) => handleSwitchChange('isActive', checked)}
            />

            {/* Categories Section */}
            <CategoriesSection
              categories={categories}
              selectedCategories={selectedCategories}
              onCategoryChange={handleCategoryChange}
              error={errors.categories}
              isLoading={isLoadingCategories}
            />

            {/* Attributes Section */}
            <ProductAttributeSelector
              attributes={attributes || []}
              productAttributes={productAttributes}
              onAttributesChange={handleAttributesChange}
              isLoading={isLoadingAttributes}
            />
          </div>
        </div>
      </form>

      {/* Show a message about variants when creating a new product */}
      {!isEditMode && (
        <div className="mt-8 p-4 border border-muted rounded-md bg-muted/50">
          <h3 className="text-lg font-medium mb-2">Want to create product variants?</h3>
          <p className="text-muted-foreground">
            You'll be able to add variants (different sizes, colors, etc.) after saving the product.
            First, make sure to create attributes marked as "Use for variants".
          </p>
        </div>
      )}
    </div>
  );
};

export default ProductFormPage;
