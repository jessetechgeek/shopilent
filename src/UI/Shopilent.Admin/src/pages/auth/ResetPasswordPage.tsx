import React, {useState, useEffect} from 'react';
import {Link, useSearchParams} from 'react-router-dom';
import {ArrowLeft, Loader2, Check} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {useTitle} from '@/hooks/useTitle';

const ResetPasswordPage: React.FC = () => {
  useTitle('Reset Password');
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');

  const [formData, setFormData] = useState({
    password: '',
    confirmPassword: '',
  });

  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isReset, setIsReset] = useState(false);
  const [isValidToken, setIsValidToken] = useState(true);

  // Verify token on component mount
  useEffect(() => {
    const verifyToken = async () => {
      if (!token) {
        setIsValidToken(false);
        setError('Invalid or missing reset token. Please request a new password reset link.');
        return;
      }

      // Here you would typically verify the token with your API
      // For now, we'll simulate token verification
      try {
        // Simulating API call to validate token
        await new Promise(resolve => setTimeout(resolve, 500));

        // For demo purposes, consider all tokens valid except for 'invalid-token'
        if (token === 'invalid-token') {
          throw new Error('Token is invalid or expired');
        }

        setIsValidToken(true);
      } catch (err: any) {
        setIsValidToken(false);
        setError(err.message || 'Invalid or expired token. Please request a new password reset link.');
      }
    };

    verifyToken();
  }, [token]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const {name, value} = e.target;
    setFormData((prev) => ({...prev, [name]: value}));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError(null);

    // Validate passwords match
    if (formData.password !== formData.confirmPassword) {
      setError('Passwords do not match.');
      setIsLoading(false);
      return;
    }

    // Validate password strength
    if (formData.password.length < 8) {
      setError('Password must be at least 8 characters long.');
      setIsLoading(false);
      return;
    }

    try {
      // Here you would typically call your API to reset the password
      // For now, we'll simulate a successful reset after a short delay
      await new Promise((resolve) => setTimeout(resolve, 1500));

      // If successful, show the success message
      setIsReset(true);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to reset password. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  if (!isValidToken) {
    return (
      <div className="space-y-6">
        <div className="space-y-2">
          <h1 className="text-2xl font-semibold">Invalid Reset Link</h1>
          <p className="text-sm text-muted-foreground">
            The password reset link is invalid or has expired.
          </p>
        </div>

        <div className="bg-destructive/10 text-destructive p-3 rounded-md text-sm">
          {error}
        </div>

        <Button asChild className="w-full">
          <Link to="/forgot-password">Request New Reset Link</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold">Reset Password</h1>
        <p className="text-sm text-muted-foreground">
          Create a new password for your account.
        </p>
      </div>

      {error && (
        <div className="bg-destructive/10 text-destructive p-3 rounded-md text-sm">
          {error}
        </div>
      )}

      {isReset ? (
        <div className="space-y-6">
          <div
            className="flex flex-col items-center justify-center text-center space-y-2 bg-green-100 text-green-800 dark:bg-green-800/20 dark:text-green-500 p-4 rounded-md">
            <Check className="size-8"/>
            <p className="font-medium">Password Reset Successful</p>
            <p className="text-sm">
              Your password has been successfully reset. You can now login with your new password.
            </p>
          </div>
          <Button asChild className="w-full">
            <Link to="/login">Return to Login</Link>
          </Button>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <label className="text-sm font-medium" htmlFor="password">
              New Password
            </label>
            <Input
              id="password"
              name="password"
              type="password"
              placeholder="••••••••"
              value={formData.password}
              onChange={handleChange}
              required
              autoComplete="new-password"
            />
            <p className="text-xs text-muted-foreground">
              Password must be at least 8 characters.
            </p>
          </div>

          <div className="space-y-2">
            <label className="text-sm font-medium" htmlFor="confirmPassword">
              Confirm New Password
            </label>
            <Input
              id="confirmPassword"
              name="confirmPassword"
              type="password"
              placeholder="••••••••"
              value={formData.confirmPassword}
              onChange={handleChange}
              required
              autoComplete="new-password"
            />
          </div>

          <Button type="submit" className="w-full" disabled={isLoading}>
            {isLoading ? (
              <>
                <Loader2 className="size-4 animate-spin mr-2"/>
                Resetting Password...
              </>
            ) : (
              'Reset Password'
            )}
          </Button>

          <Button variant="ghost" size="sm" asChild className="w-full mt-2">
            <Link to="/login" className="flex items-center justify-center">
              <ArrowLeft className="size-4 mr-2"/>
              Back to login
            </Link>
          </Button>
        </form>
      )}
    </div>
  );
};

export default ResetPasswordPage;
