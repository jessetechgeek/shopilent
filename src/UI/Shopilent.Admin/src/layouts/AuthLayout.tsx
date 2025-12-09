import React from 'react';
import {Outlet} from 'react-router-dom';
import {cn} from '@/lib/utils';

const AuthLayout: React.FC = () => {
  return (
    <div className="flex min-h-screen flex-col">
      <div className="flex flex-1 flex-col items-center justify-center px-4 py-10">
        <div className="mx-auto w-full max-w-md">
          <div className="flex flex-col items-center space-y-2 mb-6">
            <h1 className="text-3xl font-bold">Shopilent</h1>
            <p className="text-muted-foreground">Admin Portal</p>
          </div>

          <div className={cn(
            "grid gap-6",
            "w-full rounded-lg border p-6",
            "bg-card text-card-foreground shadow"
          )}>
            <Outlet/>
          </div>
        </div>
      </div>
    </div>
  );
};

export default AuthLayout;
