interface PriceFormatterProps {
  amount: number;
  currency: string;
  className?: string;
}

export function PriceFormatter({amount, currency, className}: PriceFormatterProps) {
  const formatPrice = (amount: number, currency: string) => {
    try {
      return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: currency.toUpperCase(),
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      }).format(amount);
    } catch (error) {
      // Fallback if currency is not supported
      return `${currency.toUpperCase()} ${amount.toFixed(2)}`;
    }
  };

  return (
    <span className={className}>
            {formatPrice(amount, currency)}
        </span>
  );
}
