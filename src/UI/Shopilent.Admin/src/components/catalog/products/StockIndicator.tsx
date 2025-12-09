interface StockIndicatorProps {
    stockQuantity: number;
}

export function StockIndicator({ stockQuantity }: StockIndicatorProps) {
    if (stockQuantity > 0) {
        return <span className="text-green-600 font-medium">{stockQuantity}</span>;
    }

    return <span className="text-red-500 font-medium">Out of stock</span>;
}