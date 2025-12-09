import { Badge } from '@/components/ui/badge';

interface CategoryBadgesProps {
    categories: string[];
}

export function CategoryBadges({ categories }: CategoryBadgesProps) {
    if (!categories || categories.length === 0) {
        return <span className="text-xs text-muted-foreground">No categories</span>;
    }

    return (
        <div className="flex flex-wrap gap-1">
            {categories.map((category, index) => (
                <Badge key={index} variant="secondary" className="text-xs">
                    {category}
                </Badge>
            ))}
        </div>
    );
}