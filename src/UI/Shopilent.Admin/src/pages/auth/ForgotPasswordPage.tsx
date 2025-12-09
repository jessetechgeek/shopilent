import React, {useState} from 'react';
import {Link} from 'react-router-dom';
import {ArrowLeft, Loader2} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {useTitle} from '@/hooks/useTitle';

const ForgotPasswordPage: React.FC = () => {
  useTitle('Forgot Password');
  const [email, setEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitted, setIsSubmitted] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError(null);

    try {
      // Here you would typically call your API to request a password reset
      // For now, we'll simulate a successful request after a short delay
      await new Promise((resolve) => setTimeout(resolve, 1500));

      // If successful, show the success message
      setIsSubmitted(true);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Failed to send reset link. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <h1 className="text-2xl font-semibold">Forgot Password</h1>
        <p className="text-sm text-muted-foreground">
          Enter your email address and we'll send you a password reset link.
        </p>
      </div>

      {error && (
        <div className="bg-destructive/10 text-destructive p-3 rounded-md text-sm">
          {error}
        </div>
      )}

      {isSubmitted ? (
        <div className="space-y-6">
          <div className="bg-green-100 text-green-800 dark:bg-green-800/20 dark:text-green-500 p-4 rounded-md">
            <p className="text-sm">
              We've sent a password reset link to <strong>{email}</strong>.
              Please check your email inbox and follow the instructions to reset your password.
            </p>
          </div>
          <Button asChild className="w-full">
            <Link to="/login">Return to Login</Link>
          </Button>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <label className="text-sm font-medium" htmlFor="email">
              Email
            </label>
            <Input
              id="email"
              type="email"
              placeholder="your@email.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              autoComplete="email"
              aria-describedby="email-description"
            />
            <p id="email-description" className="text-xs text-muted-foreground">
              Enter the email address you used for your account.
            </p>
          </div>

          <Button type="submit" className="w-full" disabled={isLoading}>
            {isLoading ? (
              <>
                <Loader2 className="size-4 animate-spin mr-2"/>
                Sending link...
              </>
            ) : (
              'Send Reset Link'
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

export default ForgotPasswordPage;
