import React from 'react';
import {Database, Trash2} from 'lucide-react';
import {ToolCard} from './ToolCard';
import {administrationApi} from '@/api/administration';
import type {ClearCacheResponse} from '@/models/tools';

export const CacheManagement: React.FC = () => {
  return (
    <ToolCard<ClearCacheResponse>
      icon={<Database className="size-5"/>}
      title="Cache Management"
      description="Manage system cache to improve performance and clear stale data."
      actionTitle="Clear Application Cache"
      actionDescription="Clear all cached data including API responses, user sessions, and temporary files."
      primaryButton={{
        label: 'Clear Cache',
        icon: Trash2,
        loadingText: 'Clearing...',
        variant: 'outline',
      }}
      confirmation={{
        title: 'Clear Cache?',
        description:
          'This will clear all cached data including API responses, user sessions, and temporary files. This action cannot be undone.',
        confirmLabel: 'Clear Cache',
      }}
      mutation={{
        mutationFn: () => administrationApi.clearCache(),
        onSuccessMessage: (data) =>
          data.message || 'All cached data has been cleared.',
      }}
    />
  );
};
