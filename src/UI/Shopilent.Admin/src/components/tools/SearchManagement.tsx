import React from 'react';
import {Search, RefreshCw} from 'lucide-react';
import {ToolCard} from './ToolCard';
import {administrationApi} from '@/api/administration';
import type {SearchRebuildResponse} from '@/models/tools';

export const SearchManagement: React.FC = () => {
  return (
    <ToolCard<SearchRebuildResponse>
      icon={<Search className="size-5"/>}
      title="Search Management"
      description="Configure and manage the search system for products, categories, and other searchable content."
      actionTitle="Reindex Search"
      actionDescription="Initialize search indexes and reindex all products, categories, and attributes."
      primaryButton={{
        label: 'Reindex',
        icon: RefreshCw,
        loadingText: 'Reindexing...',
        variant: 'outline',
      }}
      confirmation={{
        title: 'Reindex Search?',
        description:
          'This will initialize the search indexes and reindex all products. This operation may take several minutes depending on the amount of content.',
        confirmLabel: 'Reindex',
      }}
      mutation={{
        mutationFn: () =>
          administrationApi.searchRebuild({
            initializeIndexes: true,
            indexProducts: true,
          }),
        onSuccessMessage: (data) => {
          const {indexesInitialized, productsIndexed} = data;

          if (indexesInitialized && productsIndexed > 0) {
            return `Indexes initialized and ${productsIndexed} products indexed.`;
          } else if (indexesInitialized) {
            return 'Search indexes have been initialized.';
          } else if (productsIndexed > 0) {
            return `Successfully indexed ${productsIndexed} products.`;
          }

          return data.message || 'Search setup completed successfully.';
        },
      }}
    />
  );
};
