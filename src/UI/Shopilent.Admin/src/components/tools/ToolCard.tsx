import React, {useState} from 'react';
import {useMutation, useQueryClient} from '@tanstack/react-query';
import {Loader2} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
  TooltipProvider,
} from '@/components/ui/tooltip';
import {toast} from '@/components/ui/use-toast';
import type {ApiResponse} from '@/api/config';

interface ToolCardProps<TData = unknown> {
  // Card Header
  icon: React.ReactNode;
  title: string;
  description: string;

  // Action Section
  actionTitle: string;
  actionDescription: string;

  // Primary Button
  primaryButton: {
    label: string;
    icon: React.ComponentType<{ className?: string }>;
    loadingText: string;
    variant?: 'default' | 'outline' | 'destructive';
  };

  // Confirmation Dialog
  confirmation: {
    title: string;
    description: string;
    confirmLabel?: string;
  };

  // API Mutation
  mutation: {
    mutationFn: () => Promise<ApiResponse<TData>>;
    onSuccessMessage: (data: TData) => string;
    invalidateQueries?: string[];
  };

  // Optional Secondary Action
  secondaryAction?: {
    label: string;
    icon: React.ComponentType<{ className?: string }>;
    onClick: () => void;
    disabled?: boolean;
    tooltip?: string;
  };
}

export function ToolCard<TData = unknown>(props: ToolCardProps<TData>) {
  const [dialogOpen, setDialogOpen] = useState(false);
  const queryClient = useQueryClient();

  const mutation = useMutation({
    mutationFn: props.mutation.mutationFn,
    onSuccess: (response) => {
      const message = props.mutation.onSuccessMessage(response.data);
      toast({
        title: 'Success',
        description: message,
      });
      setDialogOpen(false);

      if (props.mutation.invalidateQueries?.length) {
        props.mutation.invalidateQueries.forEach((query) => {
          queryClient.invalidateQueries({queryKey: [query]});
        });
      }
    },
    onError: (error: Error) => {
      const message =
        (error as any).response?.data?.message || 'Operation failed';
      toast({
        title: 'Error',
        description: message,
        variant: 'destructive',
      });
      setDialogOpen(false);
    },
  });

  const PrimaryIcon = props.primaryButton.icon;
  const SecondaryIcon = props.secondaryAction?.icon;

  return (
    <>
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            {props.icon}
            {props.title}
          </CardTitle>
          <CardDescription>{props.description}</CardDescription>
        </CardHeader>

        <CardContent className="space-y-4">
          <div>
            <h4 className="font-medium">{props.actionTitle}</h4>
            <p className="text-sm text-muted-foreground">
              {props.actionDescription}
            </p>
          </div>

          <div className="space-y-2">
            <Button
              onClick={() => setDialogOpen(true)}
              disabled={mutation.isPending}
              className="w-full"
              variant={props.primaryButton.variant || 'outline'}
            >
              {mutation.isPending ? (
                <>
                  <Loader2 className="size-4 mr-2 animate-spin"/>
                  {props.primaryButton.loadingText}
                </>
              ) : (
                <>
                  <PrimaryIcon className="size-4 mr-2"/>
                  {props.primaryButton.label}
                </>
              )}
            </Button>

            {props.secondaryAction && SecondaryIcon && (
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <Button
                      onClick={props.secondaryAction.onClick}
                      disabled={props.secondaryAction.disabled}
                      className="w-full"
                      variant="outline"
                    >
                      <SecondaryIcon className="size-4 mr-2"/>
                      {props.secondaryAction.label}
                    </Button>
                  </TooltipTrigger>
                  {props.secondaryAction.tooltip && (
                    <TooltipContent>
                      <p>{props.secondaryAction.tooltip}</p>
                    </TooltipContent>
                  )}
                </Tooltip>
              </TooltipProvider>
            )}
          </div>
        </CardContent>
      </Card>

      <AlertDialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{props.confirmation.title}</AlertDialogTitle>
            <AlertDialogDescription>
              {props.confirmation.description}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={() => mutation.mutate()}>
              {props.confirmation.confirmLabel || 'Confirm'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
