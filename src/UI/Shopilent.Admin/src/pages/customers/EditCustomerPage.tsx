import React, {useState, useEffect} from 'react';
import {useParams, useNavigate} from 'react-router-dom';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {ArrowLeft, Save, User, Shield, CalendarDays} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card';
import {FormField} from '@/components/ui/form-field';
import {Input} from '@/components/ui/input';
import {Select, SelectContent, SelectItem, SelectTrigger, SelectValue} from '@/components/ui/select';
import {Switch} from '@/components/ui/switch';
import {Badge} from '@/components/ui/badge';
import {toast} from '@/components/ui/use-toast';
import {Loading} from '@/components/ui/loading';
import {useForm} from '@/hooks/useForm';
import {useApiMutation} from '@/hooks/useApiMutation';
import {customerApi} from '@/api/customers';
import {
  UpdateUserRequest,
  UpdateUserStatusRequest,
  UpdateUserRoleRequest,
  UserRole
} from '@/models/customers';
import {useTitle} from '@/hooks/useTitle';

interface EditCustomerFormData {
  firstName: string;
  lastName: string;
  phone: string;
  email: string;
  isActive: boolean;
  role: UserRole;
}

const EditCustomerPage: React.FC = () => {
  useTitle('Edit Customer');
  const {id} = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  // State for tracking what fields have changed
  const [hasChanges, setHasChanges] = useState(false);

  // Fetch customer data
  const {
    data: customer,
    isLoading: isLoadingCustomer,
    error: customerError
  } = useQuery({
    queryKey: ['customer', id],
    queryFn: async () => {
      if (!id) return null;
      const response = await customerApi.getCustomerById(id);
      if (response.data.succeeded) {
        return response.data.data;
      }
      throw new Error(response.data.message || 'Failed to fetch customer');
    },
    enabled: !!id
  });

  // Update customer mutation
  const updateCustomerMutation = useApiMutation({
    mutationFn: ({id, data}: { id: string; data: Record<string, unknown> }) =>
      customerApi.updateCustomer(id, data),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Customer updated successfully'
      });
      queryClient.invalidateQueries({queryKey: ['customers']});
      queryClient.invalidateQueries({queryKey: ['customer', id]});
      setHasChanges(false);
    }
  });

  // Update customer status mutation
  const updateStatusMutation = useApiMutation({
    mutationFn: ({id, data}: { id: string; data: UpdateUserStatusRequest }) =>
      customerApi.updateCustomerStatus(id, data),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Customer status updated successfully'
      });
      queryClient.invalidateQueries({queryKey: ['customers']});
      queryClient.invalidateQueries({queryKey: ['customer', id]});
      setHasChanges(false);
    }
  });

  // Update customer role mutation
  const updateRoleMutation = useApiMutation({
    mutationFn: ({id, data}: { id: string; data: UpdateUserRoleRequest }) =>
      customerApi.updateCustomerRole(id, data),
    onSuccess: () => {
      toast({
        title: 'Success',
        description: 'Customer role updated successfully'
      });
      queryClient.invalidateQueries({queryKey: ['customers']});
      queryClient.invalidateQueries({queryKey: ['customer', id]});
      setHasChanges(false);
    }
  });

  // Form management
  const {
    values: formData,
    errors,
    isSubmitting,
    handleChange,
    handleSwitchChange,
    setValue,
    setValues,
    handleSubmit
  } = useForm<EditCustomerFormData>({
    initialValues: {
      firstName: '',
      lastName: '',
      phone: '',
      email: '',
      isActive: true,
      role: UserRole.Customer
    },
    onSubmit: async (values) => {
      if (!id || !customer) return;

      try {
        // Update basic customer information if changed
        const basicFieldsChanged = (
          values.firstName !== customer.firstName ||
          values.lastName !== customer.lastName ||
          values.phone !== (customer.phone || '') ||
          values.email !== customer.email
        );

        if (basicFieldsChanged) {
          const updateData: UpdateUserRequest = {
            firstName: values.firstName,
            lastName: values.lastName,
            phone: values.phone || undefined,
            email: values.email
          };
          await updateCustomerMutation.mutateAsync({id, data: updateData as Record<string, unknown>});
        }

        // Update status if changed
        if (values.isActive !== customer.isActive) {
          await updateStatusMutation.mutateAsync({
            id,
            data: {isActive: values.isActive}
          });
        }

        // Update role if changed
        if (values.role !== customer.role) {
          await updateRoleMutation.mutateAsync({
            id,
            data: {role: values.role}
          });
        }

        setHasChanges(false);
      } catch (error) {
        console.error('Form submission error:', error);
      }
    },
    validate: (values) => {
      const errors: Record<string, string> = {};

      if (!values.firstName.trim()) {
        errors.firstName = 'First name is required';
      }

      if (!values.lastName.trim()) {
        errors.lastName = 'Last name is required';
      }

      if (!values.email.trim()) {
        errors.email = 'Email is required';
      } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(values.email)) {
        errors.email = 'Please enter a valid email address';
      }

      return errors;
    }
  });

  // Initialize form data when customer is loaded
  useEffect(() => {
    if (customer) {
      setValues({
        firstName: customer.firstName || '',
        lastName: customer.lastName || '',
        phone: customer.phone || '',
        email: customer.email || '',
        isActive: customer.isActive,
        role: customer.role
      });
    }
  }, [customer, setValues]);

  // Track changes to enable/disable save button
  useEffect(() => {
    if (!customer) return;

    const hasBasicChanges = (
      formData.firstName !== customer.firstName ||
      formData.lastName !== customer.lastName ||
      formData.phone !== (customer.phone || '') ||
      formData.email !== customer.email
    );
    const hasStatusChanged = formData.isActive !== customer.isActive;
    const hasRoleChanged = formData.role !== customer.role;

    setHasChanges(hasBasicChanges || hasStatusChanged || hasRoleChanged);
  }, [formData, customer]);

  // Handle role change
  const handleRoleChange = (value: string) => {
    const roleValue = UserRole[value as keyof typeof UserRole];
    setValue('role', roleValue);
  };

  // Format date for display
  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  // Get role options
  const getRoleOptions = () => {
    return Object.keys(UserRole)
      .filter(key => isNaN(Number(key)))
      .map(roleName => ({
        value: roleName,
        label: roleName
      }));
  };

  // Loading state
  if (isLoadingCustomer) {
    return <Loading text="Loading customer data..."/>;
  }

  // Error state
  if (customerError || !customer) {
    return (
      <div className="flex items-center justify-center h-64">
        <div className="text-center">
          <User className="size-8 mx-auto mb-4 text-muted-foreground"/>
          <p className="text-lg font-medium">Customer not found</p>
          <p className="text-muted-foreground">The customer you're looking for doesn't exist.</p>
          <Button onClick={() => navigate('/customers')} className="mt-4">
            <ArrowLeft className="size-4 mr-2"/>
            Back to Customers
          </Button>
        </div>
      </div>
    );
  }

  const customerIdDisplay = customer.id?.length > 8 ? customer.id.slice(-8).toUpperCase() : customer.id;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">
            Edit Customer #{customerIdDisplay}
          </h1>
          <p className="text-muted-foreground">
            Update customer information and settings
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => navigate('/customers')}
        >
          <ArrowLeft className="size-4 mr-2"/>
          Back to Customers
        </Button>
      </div>

      <form onSubmit={handleSubmit} className="space-y-6">
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Left Column - Customer Details */}
          <div className="lg:col-span-2 space-y-6">
            {/* Personal Information */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center">
                  <User className="size-5 mr-2"/>
                  Personal Information
                </CardTitle>
                <CardDescription>
                  Update the customer's basic information
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <FormField
                    label="First Name"
                    htmlFor="firstName"
                    error={errors.firstName}
                    required
                  >
                    <Input
                      id="firstName"
                      name="firstName"
                      value={formData.firstName}
                      onChange={handleChange}
                      placeholder="Enter first name"
                    />
                  </FormField>

                  <FormField
                    label="Last Name"
                    htmlFor="lastName"
                    error={errors.lastName}
                    required
                  >
                    <Input
                      id="lastName"
                      name="lastName"
                      value={formData.lastName}
                      onChange={handleChange}
                      placeholder="Enter last name"
                    />
                  </FormField>
                </div>

                <FormField
                  label="Email Address"
                  htmlFor="email"
                  error={errors.email}
                  required
                >
                  <Input
                    id="email"
                    name="email"
                    type="email"
                    value={formData.email}
                    onChange={handleChange}
                    placeholder="Enter email address"
                  />
                </FormField>

                <FormField
                  label="Phone Number"
                  htmlFor="phone"
                  description="Optional phone number for the customer"
                >
                  <Input
                    id="phone"
                    name="phone"
                    type="tel"
                    value={formData.phone}
                    onChange={handleChange}
                    placeholder="Enter phone number"
                  />
                </FormField>
              </CardContent>
            </Card>

            {/* Account Settings */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center">
                  <Shield className="size-5 mr-2"/>
                  Account Settings
                </CardTitle>
                <CardDescription>
                  Manage customer role and account status
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <FormField
                  label="Customer Role"
                  htmlFor="role"
                  description="The role determines what permissions the customer has"
                >
                  <Select
                    value={Object.keys(UserRole)[Object.values(UserRole).indexOf(formData.role)]}
                    onValueChange={handleRoleChange}
                  >
                    <SelectTrigger>
                      <SelectValue/>
                    </SelectTrigger>
                    <SelectContent>
                      {getRoleOptions().map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          {option.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FormField>

                <FormField
                  label="Account Status"
                  description="Active customers can access their account and place orders"
                >
                  <div className="flex items-center space-x-2">
                    <Switch
                      id="isActive"
                      checked={formData.isActive}
                      onCheckedChange={(checked) => handleSwitchChange('isActive', checked)}
                    />
                    <label htmlFor="isActive" className="text-sm font-medium">
                      {formData.isActive ? 'Active' : 'Inactive'}
                    </label>
                  </div>
                </FormField>

                <div className="flex items-center space-x-4 p-4 bg-muted rounded-lg">
                  <div>
                    <p className="text-sm font-medium">Current Settings:</p>
                    <div className="flex items-center space-x-2 mt-1">
                      <Badge variant={customer.isActive ? 'default' : 'secondary'}>
                        {customer.isActive ? 'Active' : 'Inactive'}
                      </Badge>
                      <Badge variant="outline">
                        {customer.roleName}
                      </Badge>
                    </div>
                  </div>
                </div>
              </CardContent>
            </Card>
          </div>

          {/* Right Column - Customer Summary */}
          <div className="space-y-6">
            {/* Customer Summary */}
            <Card>
              <CardHeader>
                <CardTitle>Customer Summary</CardTitle>
                <CardDescription>
                  Overview of customer information
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="space-y-3">
                  <div>
                    <p className="text-sm font-medium">Full Name</p>
                    <p className="text-sm text-muted-foreground">
                      {customer.fullName}
                    </p>
                  </div>

                  <div>
                    <p className="text-sm font-medium">Email</p>
                    <p className="text-sm text-muted-foreground">
                      {customer.email}
                    </p>
                  </div>

                  <div>
                    <p className="text-sm font-medium">Customer ID</p>
                    <p className="text-sm text-muted-foreground font-mono">
                      {customerIdDisplay}
                    </p>
                  </div>

                  {customer.phone && (
                    <div>
                      <p className="text-sm font-medium">Phone</p>
                      <p className="text-sm text-muted-foreground">
                        {customer.phone}
                      </p>
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>

            {/* Account Information */}
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center">
                  <CalendarDays className="size-5 mr-2"/>
                  Account Information
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3">
                <div>
                  <p className="text-sm font-medium">Member Since</p>
                  <p className="text-sm text-muted-foreground">
                    {formatDate(customer.createdAt)}
                  </p>
                </div>

                <div>
                  <p className="text-sm font-medium">Last Updated</p>
                  <p className="text-sm text-muted-foreground">
                    {formatDate(customer.updatedAt)}
                  </p>
                </div>

                {customer.emailVerified !== undefined && (
                  <div>
                    <p className="text-sm font-medium">Email Verified</p>
                    <Badge variant={customer.emailVerified ? 'default' : 'secondary'}>
                      {customer.emailVerified ? 'Verified' : 'Not Verified'}
                    </Badge>
                  </div>
                )}

                {customer.lastLogin && (
                  <div>
                    <p className="text-sm font-medium">Last Login</p>
                    <p className="text-sm text-muted-foreground">
                      {formatDate(customer.lastLogin)}
                    </p>
                  </div>
                )}
              </CardContent>
            </Card>

            {/* Save Button */}
            <Button
              type="submit"
              disabled={!hasChanges || isSubmitting}
              className="w-full"
              size="lg"
            >
              <Save className="size-4 mr-2"/>
              {isSubmitting ? 'Saving...' : 'Save Changes'}
            </Button>
          </div>
        </div>
      </form>
    </div>
  );
};

export default EditCustomerPage;
