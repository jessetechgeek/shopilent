import {useState} from 'react';

interface FormOptions<T> {
  initialValues: T;
  onSubmit: (values: T) => Promise<void>;
  validate?: (values: T) => Record<string, string>;
}

export function useForm<T extends Record<string, any>>({
                                                         initialValues,
                                                         onSubmit,
                                                         validate
                                                       }: FormOptions<T>) {
  const [values, setValues] = useState<T>(initialValues);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleChange = (
    e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
  ) => {
    const {name, value} = e.target;
    setValues(prev => ({...prev, [name]: value}));

    // Clear error when field is changed
    if (errors[name]) {
      setErrors(prev => ({...prev, [name]: ''}));
    }
  };

  const handleNumberChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const {name, value} = e.target;
    setValues(prev => ({...prev, [name]: parseFloat(value)}));

    if (errors[name]) {
      setErrors(prev => ({...prev, [name]: ''}));
    }
  };

  const handleSwitchChange = (name: string, checked: boolean) => {
    setValues(prev => ({...prev, [name]: checked}));
  };

  const handleSelectChange = (name: string, value: string) => {
    setValues(prev => ({...prev, [name]: value}));

    if (errors[name]) {
      setErrors(prev => ({...prev, [name]: ''}));
    }
  };

  const setValue = (name: string, value: any) => {
    setValues(prev => ({...prev, [name]: value}));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (validate) {
      const validationErrors = validate(values);
      if (Object.keys(validationErrors).length > 0) {
        setErrors(validationErrors);
        return;
      }
    }

    setIsSubmitting(true);
    try {
      await onSubmit(values);
    } catch (error) {
      console.error('Form submission error:', error);
    } finally {
      setIsSubmitting(false);
    }
  };

  return {
    values,
    errors,
    isSubmitting,
    handleChange,
    handleNumberChange,
    handleSwitchChange,
    handleSelectChange,
    setValue,
    setValues,
    setErrors, // Make sure we're exposing this
    handleSubmit,
  };
}
