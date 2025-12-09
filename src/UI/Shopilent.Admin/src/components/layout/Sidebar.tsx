import React from 'react';
import {Link, useLocation} from 'react-router-dom';
import {cn} from '@/lib/utils';
import {
  LayoutDashboard,
  ShoppingBag,
  Layers,
  ShoppingCart,
  Users,
  Settings,
  LogOut,
  List,
  Wrench
} from 'lucide-react';
import {Button} from '@/components/ui/button';
import {useAuth} from '@/contexts/AuthContext';

const Sidebar: React.FC = () => {
  const location = useLocation();
  const {logout} = useAuth();

  const menuItems = [
    {
      icon: <LayoutDashboard className="size-5"/>,
      label: 'Dashboard',
      path: '/dashboard'
    },
    {
      icon: <Layers className="size-5"/>,
      label: 'Categories',
      path: '/catalog/categories'
    },
    {
      icon: <List className="size-5"/>,
      label: 'Attributes',
      path: '/catalog/attributes'
    },
    {
      icon: <ShoppingBag className="size-5"/>,
      label: 'Products',
      path: '/catalog/products'
    },
    {
      icon: <ShoppingCart className="size-5"/>,
      label: 'Orders',
      path: '/orders'
    },
    {
      icon: <Users className="size-5"/>,
      label: 'Customers',
      path: '/customers'
    },
    {
      icon: <Wrench className="size-5"/>,
      label: 'Tools',
      path: '/tools'
    },
    {
      icon: <Settings className="size-5"/>,
      label: 'Settings',
      path: '/settings'
    },
  ];

  const isActive = (path: string) => {
    if (path === '/dashboard') {
      return location.pathname === '/dashboard';
    }
    return location.pathname.startsWith(path);
  };

  const handleLogout = async () => {
    await logout();
  };

  return (
    <aside className="w-64 bg-sidebar text-sidebar-foreground border-r border-sidebar-border hidden md:flex flex-col">

      {/* Logo */}
      <div className="p-6">
        <Link to="/dashboard" className="flex items-center space-x-2">
          <span className="text-2xl font-bold">Shopilent</span>
        </Link>
      </div>

      {/* Navigation */}
      <nav className="flex-1 py-4 px-4">
        <div className="space-y-1">
          {menuItems.map((item) => (
            <Link
              key={item.path}
              to={item.path}
              className={cn(
                "flex items-center px-3 py-2 rounded-md text-sm font-medium transition-colors",
                isActive(item.path)
                  ? "bg-sidebar-accent text-sidebar-accent-foreground"
                  : "text-sidebar-foreground hover:bg-sidebar-accent/50 hover:text-sidebar-accent-foreground"
              )}
            >
              {item.icon}
              <span className="ml-3">{item.label}</span>
            </Link>
          ))}
        </div>
      </nav>

      {/* Footer with logout */}
      <div className="p-4 border-t border-sidebar-border">
        <Button
          variant="ghost"
          className="w-full justify-start text-sidebar-foreground"
          onClick={handleLogout}
        >
          <LogOut className="size-5"/>
          <span className="ml-3">Logout</span>
        </Button>
      </div>
    </aside>
  );
};

export default Sidebar;
