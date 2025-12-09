import React from 'react';
import {Navigate, Outlet} from 'react-router-dom';
import {useAuth} from '../contexts/AuthContext';
import {UserRole} from '../models/auth';

interface ProtectedRouteProps {
  children?: React.ReactNode;
  requiredRoles?: UserRole[];
}

const ProtectedRoute: React.FC<ProtectedRouteProps> = ({
                                                         children,
                                                         requiredRoles = [UserRole.Admin, UserRole.Manager]
                                                       }) => {
  const {isAuthenticated, hasRole, isLoading} = useAuth();

  // If auth is still being loaded, show a loading state
  if (isLoading) {
    return <div className="flex h-screen items-center justify-center">Loading...</div>;
  }

  // Check if user is authenticated
  if (!isAuthenticated) {
    return <Navigate to="/login" replace/>;
  }

  // Check if user has required role
  if (requiredRoles.length > 0 && !hasRole(requiredRoles)) {
    return <Navigate to="/unauthorized" replace/>;
  }

  // If there are children, render them, otherwise render the Outlet
  return children ? <>{children}</> : <Outlet/>;
};

export default ProtectedRoute;
