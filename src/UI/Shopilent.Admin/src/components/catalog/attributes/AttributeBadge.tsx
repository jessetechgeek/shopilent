import { Badge } from '@/components/ui/badge';
import { AttributeType } from '@/models/catalog';

interface AttributeBadgeProps {
    type: AttributeType;
}

export function AttributeBadge({ type }: AttributeBadgeProps) {
    let variant: "default" | "secondary" | "destructive" | "outline" = "default";

    switch (type) {
        case AttributeType.Text:
            variant = "outline";
            break;
        case AttributeType.Number:
        case AttributeType.Dimensions:
        case AttributeType.Weight:
            variant = "secondary";
            break;
        case AttributeType.Boolean:
            variant = "outline";
            break;
        case AttributeType.Select:
            variant = "default";
            break;
        case AttributeType.Color:
            variant = "default";
            break;
        case AttributeType.Date:
            variant = "outline";
            break;
        default:
            variant = "outline";
    }

    return (
        <Badge variant={variant} className="whitespace-nowrap">
            {type}
        </Badge>
    );
}