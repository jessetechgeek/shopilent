import {Loader2} from 'lucide-react';
import {cn} from '@/lib/utils';

interface LoadingProps {
  text?: string;
  size?: 'sm' | 'md' | 'lg';
  fullPage?: boolean;
  className?: string;
}

export function Loading({
                          text = 'Loading...',
                          size = 'md',
                          fullPage = false,
                          className,
                        }: LoadingProps) {
  const sizes = {
    sm: 'h-4 w-4',
    md: 'h-8 w-8',
    lg: 'h-12 w-12',
  };

  const content = (
    <div className={cn(
      "flex flex-col items-center justify-center",
      fullPage ? "min-h-[calc(100vh-4rem)]" : "min-h-[200px]",
      className
    )}>
      <Loader2 className={cn(
        sizes[size],
        "animate-spin text-primary mb-2"
      )}/>
      {text && <p className="text-muted-foreground">{text}</p>}
    </div>
  );

  return content;
}
