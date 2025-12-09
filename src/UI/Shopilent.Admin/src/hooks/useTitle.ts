import {useEffect} from 'react';

/**
 * Custom hook to set the document title
 * @param title - The page-specific title (will be suffixed with " | Shopilent Admin")
 * @example
 * useTitle('Products'); // Sets title to "Products | Shopilent Admin"
 */
export function useTitle(title: string): void {
  useEffect(() => {
    const prevTitle = document.title;
    document.title = title ? `${title} | Shopilent` : 'Shopilent Admin';

    return () => {
      document.title = prevTitle;
    };
  }, [title]);
}
