import {useMutation, useQueryClient} from '@tanstack/react-query';
import {toast} from '@/components/ui/use-toast';

interface ApiMutationOptions<TData, TError, TVariables, TContext> {
  mutationFn: (variables: TVariables) => Promise<TData>;
  onSuccessMessage?: string;
  invalidateQueries?: string[];
  onSuccess?: (data: TData, variables: TVariables, context: TContext | undefined) => void;
  onError?: (error: TError, variables: TVariables, context: TContext | undefined) => void;
}

export function useApiMutation<TData, TError, TVariables, TContext>({
                                                                      mutationFn,
                                                                      onSuccessMessage,
                                                                      invalidateQueries = [],
                                                                      onSuccess,
                                                                      onError,
                                                                    }: ApiMutationOptions<TData, TError, TVariables, TContext>) {
  const queryClient = useQueryClient();

  return useMutation<TData, TError, TVariables, TContext>({
    mutationFn,
    onSuccess: (data, variables, context) => {
      // Show success toast if message provided
      if (onSuccessMessage) {
        toast({
          title: 'Success',
          description: onSuccessMessage,
          variant: 'success'
        });
      }

      // Invalidate queries if specified
      if (invalidateQueries.length > 0) {
        invalidateQueries.forEach(query => {
          queryClient.invalidateQueries({queryKey: [query]});
        });
      }

      // Call additional onSuccess handler if provided
      if (onSuccess) {
        onSuccess(data, variables, context);
      }
    },
    onError: (error: TError, variables, context) => {
      // Extract error message safely
      const getErrorMessage = (err: unknown): string => {
        if (err && typeof err === 'object') {
          // Handle Axios-style errors
          if ('response' in err && err.response &&
            typeof err.response === 'object' &&
            'data' in err.response &&
            err.response.data &&
            typeof err.response.data === 'object' &&
            'message' in err.response.data &&
            typeof err.response.data.message === 'string') {
            return err.response.data.message;
          }
          // Handle standard Error objects
          if ('message' in err && typeof err.message === 'string') {
            return err.message;
          }
        }
        return 'An error occurred';
      };

      // Show error toast
      toast({
        title: 'Error',
        description: getErrorMessage(error),
        variant: 'destructive'
      });

      // Call additional onError handler if provided
      if (onError) {
        onError(error, variables, context);
      }
    }
  });
}
