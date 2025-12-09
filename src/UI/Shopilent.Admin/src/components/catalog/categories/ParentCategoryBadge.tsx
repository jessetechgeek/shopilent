import {Folder, FolderPlus} from 'lucide-react';
import {CategoryDto} from '@/models/catalog';
import {Button} from '@/components/ui/button';

interface ParentCategoryBadgeProps {
  parentName?: string;
  onParentClick: (category: CategoryDto) => void;
  category: CategoryDto;
}

export function ParentCategoryBadge({
                                      parentName,
                                      onParentClick,
                                      category
                                    }: ParentCategoryBadgeProps) {
  if (parentName) {
    return (
      <Button
        variant="ghost"
        size="sm"
        onClick={() => onParentClick(category)}
        className="h-7 px-2 text-xs font-normal hover:bg-accent"
      >
        <Folder className="size-3 mr-1.5"/>
        {parentName}
      </Button>
    );
  }

  return (
    <Button
      variant="outline"
      size="sm"
      onClick={() => onParentClick(category)}
      className="h-7 px-2 text-xs font-normal border-dashed hover:bg-accent"
    >
      <FolderPlus className="size-3 mr-1.5"/>
      Set parent
    </Button>
  );
}
