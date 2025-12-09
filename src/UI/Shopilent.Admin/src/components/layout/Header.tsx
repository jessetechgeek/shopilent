import React from 'react';
import {Bell, Search, User, Moon, Sun} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {Input} from '@/components/ui/input';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {useTheme} from '@/hooks/useTheme';
import {useAuth} from '@/contexts/AuthContext';
import {Link} from 'react-router-dom';

const Header: React.FC = () => {
  const {theme, setTheme} = useTheme();
  const {user, logout} = useAuth();

  const handleLogout = async () => {
    await logout();
  };

  return (
    <header className="border-b border-border bg-background">
      <div className="flex h-16 items-center justify-between px-6">
        {/* Mobile menu button - Will be implemented later */}
        <div className="md:hidden">
          {/* Mobile menu toggle button will go here */}
        </div>

        {/* Search */}
        <div className="hidden md:flex md:w-1/3 lg:w-1/4">
          <div className="relative w-full">
            <div className="absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none">
              <Search className="size-4 text-muted-foreground"/>
            </div>
            <Input
              type="search"
              placeholder="Search..."
              className="pl-10"
            />
          </div>
        </div>

        {/* Right side actions */}
        <div className="flex items-center space-x-2">
          {/* Theme toggle */}
          <Button
            variant="ghost"
            size="icon"
            aria-label="Toggle theme"
            onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
          >
            {theme === 'dark' ? <Sun className="size-4"/> : <Moon className="size-4"/>}
          </Button>

          {/* Notifications */}
          <Button variant="ghost" size="icon" aria-label="Notifications">
            <Bell className="size-4"/>
          </Button>

          {/* User menu */}
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="rounded-full">
                <User className="size-4"/>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel>
                {user?.firstName ? `${user.firstName} ${user.lastName}` : 'My Account'}
              </DropdownMenuLabel>
              <DropdownMenuSeparator/>
              <DropdownMenuItem asChild>
                <Link to="/profile">Profile</Link>
              </DropdownMenuItem>
              <DropdownMenuItem asChild>
                <Link to="/settings">Settings</Link>
              </DropdownMenuItem>
              <DropdownMenuSeparator/>
              <DropdownMenuItem onClick={handleLogout}>
                Logout
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>
    </header>
  );
};

export default Header;
