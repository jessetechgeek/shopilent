interface PriceFormatterProps {
    price: number;
    currency: string;
}

export function PriceFormatter({ price, currency }: PriceFormatterProps) {
    // Get currency symbol for price formatting
    const getCurrencySymbol = (currency: string): string => {
        switch (currency) {
            case 'USD':
                return '$';
            case 'EUR':
                return '€';
            case 'GBP':
                return '£';
            case 'CAD':
                return 'CA$';
            case 'AUD':
                return 'A$';
            case 'JPY':
                return '¥';
            default:
                return '$';
        }
    };

    return (
        <span className="font-medium">
            {getCurrencySymbol(currency)}{price.toFixed(2)}
        </span>
    );
}