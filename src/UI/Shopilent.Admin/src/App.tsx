import {BrowserRouter} from 'react-router-dom';
import {QueryClient, QueryClientProvider} from '@tanstack/react-query';
import {ThemeProvider} from '@/hooks/useTheme';
import {AuthProvider} from '@/contexts/AuthContext';
import {Toaster} from '@/components/ui/toaster';
import AppRoutes from '@/routes';

// Create a client for React Query
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
    },
  },
});

function App() {
  return (
    <BrowserRouter>
      <ThemeProvider defaultTheme="system">
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <AppRoutes/>
            <Toaster/>
          </AuthProvider>
        </QueryClientProvider>
      </ThemeProvider>
    </BrowserRouter>
  );
}

export default App;
