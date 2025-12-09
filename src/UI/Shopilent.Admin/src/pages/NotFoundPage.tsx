import React from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { useTitle } from '@/hooks/useTitle';

const NotFoundPage: React.FC = () => {
    useTitle('Page Not Found');
    return (
        <div className="flex flex-col items-center justify-center min-h-screen p-4 text-center">
            <h1 className="text-9xl font-bold text-primary">404</h1>
            <h2 className="mt-4 text-3xl font-semibold">Page Not Found</h2>
            <p className="mt-2 text-lg text-muted-foreground max-w-md">
                The page you are looking for doesn't exist or has been moved.
            </p>
            <Button asChild className="mt-8">
                <Link to="/dashboard">Back to Dashboard</Link>
            </Button>
        </div>
    );
};

export default NotFoundPage;