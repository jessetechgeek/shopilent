import React from 'react';
import {ShoppingBag, Users, ShoppingCart, DollarSign, ArrowUp, ArrowDown, Loader2, ArrowRight} from 'lucide-react';
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {useAuth} from '@/contexts/AuthContext';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from 'react-router-dom';
import {orderApi} from '@/api/orders';
import {OrderStatusBadge} from '@/components/orders/OrderStatusBadge';
import {PriceFormatter} from '@/components/orders/PriceFormatter';
import {useTitle} from '@/hooks/useTitle';

const DashboardPage: React.FC = () => {
  useTitle('Dashboard');
  const {user} = useAuth();
  const navigate = useNavigate();

  // Fetch recent orders
  const {data: recentOrdersData, isLoading: recentOrdersLoading, error: recentOrdersError} = useQuery({
    queryKey: ['recentOrders'],
    queryFn: () => orderApi.getRecentOrders(),
    select: (response) => response.data.data,
    staleTime: 2 * 60 * 1000, // 2 minutes
    refetchInterval: 5 * 60 * 1000, // Refetch every 5 minutes
  });

  // Sample data for demonstration
  const stats = [
    {
      title: 'Total Revenue',
      value: '$45,231.89',
      change: '+20.1%',
      trend: 'up',
      icon: DollarSign,
    },
    {
      title: 'Orders',
      value: '356',
      change: '+12.2%',
      trend: 'up',
      icon: ShoppingCart,
    },
    {
      title: 'Products',
      value: '623',
      change: '+4.9%',
      trend: 'up',
      icon: ShoppingBag,
    },
    {
      title: 'Customers',
      value: '1,203',
      change: '-3.2%',
      trend: 'down',
      icon: Users,
    },
  ];

  // Format date for display
  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Dashboard</h1>
        <p className="text-muted-foreground">
          Welcome back, {user?.firstName || 'Admin'}!
        </p>
      </div>

      {/* Stats Cards */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {stats.map((stat, index) => (
          <Card key={index}>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">{stat.title}</CardTitle>
              <stat.icon className="size-4 text-muted-foreground"/>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{stat.value}</div>
              <p className="flex items-center text-xs text-muted-foreground mt-1">
                {stat.trend === 'up' ? (
                  <ArrowUp className="size-3 mr-1 text-green-500"/>
                ) : (
                  <ArrowDown className="size-3 mr-1 text-red-500"/>
                )}
                <span className={stat.trend === 'up' ? 'text-green-500' : 'text-red-500'}>
                  {stat.change}
                </span>
                <span className="ml-1">from last month</span>
              </p>
            </CardContent>
          </Card>
        ))}
      </div>

      {/* Recent Orders */}
      <Card>
        <CardHeader>
          <CardTitle>Recent Orders</CardTitle>
          <CardDescription>
            {recentOrdersData ?
              `${recentOrdersData.count} most recent orders from your store` :
              'A list of recent orders from your store'
            }
          </CardDescription>
        </CardHeader>
        <CardContent>
          {recentOrdersLoading ? (
            <div className="flex items-center justify-center h-32">
              <Loader2 className="h-6 w-6 animate-spin"/>
              <span className="ml-2">Loading recent orders...</span>
            </div>
          ) : recentOrdersError ? (
            <div className="flex items-center justify-center h-32 text-muted-foreground">
              <p>Unable to load recent orders. Please try again later.</p>
            </div>
          ) : !recentOrdersData?.orders?.length ? (
            <div className="flex items-center justify-center h-32 text-muted-foreground">
              <p>No recent orders found.</p>
            </div>
          ) : (
            <div className="overflow-hidden rounded-md border">
              <table className="w-full text-sm">
                <thead>
                <tr className="bg-muted border-b">
                  <th className="h-10 px-4 text-left font-medium">Order</th>
                  <th className="h-10 px-4 text-left font-medium">Customer</th>
                  <th className="h-10 px-4 text-left font-medium">Date</th>
                  <th className="h-10 px-4 text-left font-medium">Total</th>
                  <th className="h-10 px-4 text-left font-medium">Status</th>
                </tr>
                </thead>
                <tbody>
                {recentOrdersData.orders.map((order, index) => (
                  <tr
                    key={order.id}
                    className={index % 2 === 0 ? 'bg-background' : 'bg-muted/50'}
                  >
                    <td className="p-4 align-middle">
                                                <span className="font-mono text-sm">
                                                    #{order.id.slice(-8).toUpperCase()}
                                                </span>
                    </td>
                    <td className="p-4 align-middle">
                      <div>
                        <div className="font-medium">{order.userFullName}</div>
                        <div className="text-xs text-muted-foreground">{order.userEmail}</div>
                      </div>
                    </td>
                    <td className="p-4 align-middle">
                      {formatDate(order.createdAt)}
                    </td>
                    <td className="p-4 align-middle">
                      <PriceFormatter
                        amount={order.total}
                        currency={order.currency}
                      />
                    </td>
                    <td className="p-4 align-middle">
                      <OrderStatusBadge status={order.status}/>
                    </td>
                  </tr>
                ))}
                </tbody>
              </table>
              {recentOrdersData?.orders && recentOrdersData.orders.length > 0 && (
                <div className="border-t p-4">
                  <Button
                    variant="ghost"
                    className="w-full justify-between"
                    onClick={() => navigate('/orders')}
                  >
                    View all orders
                    <ArrowRight className="h-4 w-4"/>
                  </Button>
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
};

export default DashboardPage;
