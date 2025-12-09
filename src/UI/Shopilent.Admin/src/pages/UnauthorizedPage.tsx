import React from 'react';
import { Link } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { ShieldAlert } from 'lucide-react';
import { useTitle } from '@/hooks/useTitle';

const UnauthorizedPage: React.FC = () => {
    useTitle('Unauthorized');
    return (
        <div className="flex flex-col items-center justify-center min-h-screen p-4 text-center">
            <ShieldAlert className="size-20 text-destructive mb-6" />
            <h1 className="text-3xl font-bold">Access Denied</h1>
            <p className="mt-4 text-lg text-muted-foreground max-w-md">
                You don't have permission to access this page. Please contact your administrator if you
                believe this is an error.
            </p>
            <Button asChild className="mt-8">
                <Link to="/dashboard">Back to Dashboard</Link>
            </Button>
        </div>
    );
};

export default UnauthorizedPage;