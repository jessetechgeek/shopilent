import {ShoppingCart} from 'lucide-react';

interface OrderEmptyStateProps {
  searchQuery?: string;
}

export function OrderEmptyState({searchQuery}: OrderEmptyStateProps) {
  if (searchQuery) {
    return (
      <div className="flex flex-col items-center justify-center py-12 text-center">
        <ShoppingCart className="h-12 w-12 text-muted-foreground mb-4"/>
        <h3 className="text-lg font-semibold text-foreground mb-2">
          No orders found
        </h3>
        <p className="text-muted-foreground mb-4 max-w-md">
          No orders match your search for "{searchQuery}". Try adjusting your search terms.
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-center justify-center py-12 text-center">
      <ShoppingCart className="h-12 w-12 text-muted-foreground mb-4"/>
      <h3 className="text-lg font-semibold text-foreground mb-2">
        No orders yet
      </h3>
      <p className="text-muted-foreground mb-4 max-w-md">
        Orders will appear here once customers start placing them.
        Start promoting your products to get your first orders!
      </p>
    </div>
  );
}
