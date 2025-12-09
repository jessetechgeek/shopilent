import React from 'react';
import {Wrench} from 'lucide-react';
import {SearchManagement} from '@/components/tools/SearchManagement';
import {CacheManagement} from '@/components/tools/CacheManagement';
import {useTitle} from '@/hooks/useTitle';

const ToolsPage: React.FC = () => {
  useTitle('Tools');
  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Wrench className="size-8 text-muted-foreground"/>
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Tools</h1>
          <p className="text-muted-foreground">
            Administrative tools for managing search indexing and system cache
          </p>
        </div>
      </div>

      {/* Management Sections */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <SearchManagement/>
        <CacheManagement/>
      </div>
    </div>
  );
};

export default ToolsPage;
