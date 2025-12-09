import { Tag } from 'lucide-react';
import { Badge } from '@/components/ui/badge';

interface SkuBadgeProps {
    sku: string | undefined;
}

export function SkuBadge({ sku }: SkuBadgeProps) {
    if (!sku) {
        return <span className="text-xs text-muted-foreground">No SKU</span>;
    }

    return (
        <Badge variant="outline" className="font-mono">
            <Tag className="size-3 mr-1" />
            {sku}
        </Badge>
    );
}