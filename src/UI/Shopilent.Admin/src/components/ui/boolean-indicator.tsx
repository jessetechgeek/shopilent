import { Check, X } from 'lucide-react';

interface BooleanIndicatorProps {
    value: boolean;
}

export function BooleanIndicator({ value }: BooleanIndicatorProps) {
    if (value) {
        return <Check className="size-4 inline-block text-green-500" />;
    }
    return <X className="size-4 inline-block text-muted-foreground" />;
}