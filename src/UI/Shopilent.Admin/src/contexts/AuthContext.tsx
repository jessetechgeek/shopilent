import React, {createContext, useContext, useState, useEffect} from 'react';
import {useNavigate} from 'react-router-dom';
import {authApi} from '../api/auth';
import {User, LoginRequest, UserRole} from '../models/auth';

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  login: (data: LoginRequest) => Promise<void>;
  logout: () => Promise<void>;
  isLoading: boolean;
  error: string | null;
  hasRole: (roles: UserRole[]) => boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({children}) => {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const navigate = useNavigate();

  // Check if user is already logged in
  useEffect(() => {
    const checkAuth = async () => {
      setIsLoading(true);
      const storedUser = localStorage.getItem('user');

      if (storedUser) {
        try {
          // Set user from localStorage
          const userData = JSON.parse(storedUser);

          // If parsed data doesn't match User interface shape, create a proper User object
          if (!userData.role) {
            // Create a properly structured user object
            const completeUserData: User = {
              id: userData.id,
              email: userData.email,
              firstName: userData.firstName || userData.fullName?.split(' ')[0] || '',
              lastName: userData.lastName || userData.fullName?.split(' ')[1] || '',
              role: UserRole.Admin, // Assuming admin role
              isActive: true,
              emailVerified: true,
              createdAt: userData.createdAt || new Date().toISOString(),
              updatedAt: userData.updatedAt || new Date().toISOString()
            };
            setUser(completeUserData);

            // Update localStorage with the complete user object
            localStorage.setItem('user', JSON.stringify(completeUserData));
          } else {
            setUser(userData);
          }
        } catch (err) {
          console.error("Error parsing stored user data:", err);
          // Clear invalid user data
          localStorage.removeItem('user');
        }
      }
      setIsLoading(false);
    };

    checkAuth();
  }, []);

  const login = async (data: LoginRequest) => {
    try {
      setIsLoading(true);
      setError(null);

      const response = await authApi.login(data);

      if (response.data.succeeded) {
        const {email, id, firstName, lastName, emailVerified} = response.data.data;

        // Create a new user object from the response data
        // Tokens are now stored in HttpOnly cookies by the backend
        const userData = {
          id,
          email,
          firstName: firstName || '',
          lastName: lastName || '',
          role: UserRole.Admin, // Assuming default role or extract from response if available
          isActive: true,
          emailVerified: emailVerified,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString()
        };

        // Store only user data in localStorage (not tokens)
        localStorage.setItem('user', JSON.stringify(userData));

        // Update state with new user data
        setUser(userData);
        navigate('/dashboard');
      } else {
        setError(response.data.message || 'Login failed');
      }
    } catch (err: any) {
      setError(err.response?.data?.message || 'An error occurred during login');
    } finally {
      setIsLoading(false);
    }
  };

  const logout = async () => {
    try {
      console.log('Logging out...');
      setIsLoading(true);

      // Call logout API - refresh token is sent via HttpOnly cookie
      await authApi.logout();
    } catch (err) {
      console.error('Error during logout:', err);
    } finally {
      // Clear user data regardless of API success
      // Cookies are cleared by the backend
      localStorage.removeItem('user');
      setUser(null);
      navigate('/login');
      setIsLoading(false);
    }
  };

  const hasRole = (roles: UserRole[]) => {
    if (!user) return false;
    return roles.includes(user.role);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user,
        login,
        logout,
        isLoading,
        error,
        hasRole,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
