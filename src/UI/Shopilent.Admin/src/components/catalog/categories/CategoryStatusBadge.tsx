import {Check, X} from 'lucide-react';

interface CategoryStatusBadgeProps {
  isActive: boolean;
}

export function CategoryStatusBadge({isActive}: CategoryStatusBadgeProps) {
  if (isActive) {
    return (
      <span
        className="inline-flex items-center rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900/30 dark:text-green-500">
                <Check className="size-3 mr-1"/>
                Active
            </span>
    );
  }

  return (
    <span
      className="inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-800 dark:bg-gray-900/30 dark:text-gray-400">
            <X className="size-3 mr-1"/>
            Inactive
        </span>
  );
}
