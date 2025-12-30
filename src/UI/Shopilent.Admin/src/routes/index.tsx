import {Routes, Route, Navigate} from 'react-router-dom';
import {useAuth} from '@/contexts/AuthContext';
import ProtectedRoute from './ProtectedRoute';
import {UserRole} from '@/models/auth';

// Layouts
import MainLayout from '@/layouts/MainLayout';
import AuthLayout from '@/layouts/AuthLayout';

// Auth Pages
import LoginPage from '@/pages/auth/LoginPage';
import ForgotPasswordPage from '@/pages/auth/ForgotPasswordPage';
import ResetPasswordPage from '@/pages/auth/ResetPasswordPage';

// Dashboard
import DashboardPage from '@/pages/dashboard/DashboardPage';

// Settings
import ProfilePage from '@/pages/settings/ProfilePage';
import SettingsPage from '@/pages/settings/SettingsPage';

// Catalog
import CategoriesPage from '@/pages/catalog/CategoriesPage';
import AttributesPage from '@/pages/catalog/AttributesPage';
import ProductsPage from '@/pages/catalog/ProductsPage';
import ProductFormPage from '@/pages/catalog/ProductFormPage'; // Add this import

// Orders
import OrdersPage from "@/pages/orders/OrdersPage.tsx";
import EditOrderPage from '@/pages/orders/EditOrderPage'; // Add this import

// Customers
import CustomersPage from '@/pages/customers/CustomersPage';
import EditCustomerPage from '@/pages/customers/EditCustomerPage';

// Tools
import ToolsPage from '@/pages/tools/ToolsPage';
import StatusPage from '@/pages/status/StatusPage';

// Not Found & Unauthorized
import NotFoundPage from '@/pages/NotFoundPage';
import UnauthorizedPage from '@/pages/UnauthorizedPage';

const AppRoutes = () => {
  const {isAuthenticated} = useAuth();

  return (
    <Routes>
      {/* Auth Routes */}
      <Route element={<AuthLayout/>}>
        <Route
          path="/login"
          element={isAuthenticated ? <Navigate to="/dashboard"/> : <LoginPage/>}
        />
        <Route path="/forgot-password" element={<ForgotPasswordPage/>}/>
        <Route path="/reset-password" element={<ResetPasswordPage/>}/>
      </Route>

      {/* Protected Routes */}
      <Route element={<ProtectedRoute/>}>
        <Route element={<MainLayout/>}>
          {/* Dashboard */}
          <Route path="/dashboard" element={<DashboardPage/>}/>

          {/* Catalog */}
          <Route path="/catalog/categories" element={<CategoriesPage/>}/>
          <Route path="/catalog/attributes" element={<AttributesPage/>}/>
          <Route path="/catalog/products" element={<ProductsPage/>}/>
          {/* Add these new routes */}
          <Route path="/catalog/products/new" element={<ProductFormPage/>}/>
          <Route path="/catalog/products/edit/:id" element={<ProductFormPage/>}/>

          {/* Orders */}
          <Route path="/orders" element={<OrdersPage/>}/>
          <Route path="/orders/edit/:id" element={<EditOrderPage/>}/> {/* Add this route */}

          {/* Customers */}
          <Route path="/customers" element={<CustomersPage/>}/>
          <Route path="/customers/edit/:id" element={<EditCustomerPage/>}/>

          {/* Tools */}
          <Route path="/tools" element={<ToolsPage/>}/>
        </Route>
      </Route>

      {/* Admin-only Routes */}
      <Route element={<ProtectedRoute requiredRoles={[UserRole.Admin]}/>}>
        <Route element={<MainLayout/>}>
          <Route path="/system-status" element={<StatusPage/>}/>

          {/* Settings */}
          <Route path="/profile" element={<ProfilePage/>}/>
          <Route path="/settings" element={<SettingsPage/>}/>
        </Route>
      </Route>

      {/* Unauthorized */}
      <Route path="/unauthorized" element={<UnauthorizedPage/>}/>

      {/* Redirect root to dashboard or login */}
      <Route
        path="/"
        element={isAuthenticated ? <Navigate to="/dashboard"/> : <Navigate to="/login"/>}
      />

      {/* Not Found */}
      <Route path="*" element={<NotFoundPage/>}/>
    </Routes>
  );
};

export default AppRoutes;
