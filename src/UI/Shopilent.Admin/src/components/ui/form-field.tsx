import React from 'react';
import {AlertCircle} from 'lucide-react';
import {cn} from '@/lib/utils';

interface FormFieldProps {
  label: string;
  children: React.ReactNode;
  error?: string;
  description?: string;
  required?: boolean;
  className?: string;
  htmlFor?: string;
}

export function FormField({
                            label,
                            children,
                            error,
                            description,
                            required,
                            className,
                            htmlFor
                          }: FormFieldProps) {
  return (
    <div className={cn("space-y-2", className)}>
      <label className="text-sm font-medium" htmlFor={htmlFor}>
        {label} {required && <span className="text-destructive">*</span>}
      </label>
      {children}
      {description && !error && (
        <p className="text-xs text-muted-foreground">{description}</p>
      )}
      {error && (
        <p className="text-xs text-destructive flex items-center">
          <AlertCircle className="size-3 mr-1"/>
          {error}
        </p>
      )}
    </div>
  );
}
