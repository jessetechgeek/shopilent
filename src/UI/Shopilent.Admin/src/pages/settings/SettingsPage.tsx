import React from 'react';
import {Card, CardContent, CardDescription, CardHeader, CardTitle} from '@/components/ui/card';
import {Button} from '@/components/ui/button';
import {Moon, Sun, Monitor} from 'lucide-react';
import {useTheme} from '@/hooks/useTheme';
import {useTitle} from '@/hooks/useTitle';

const SettingsPage: React.FC = () => {
  useTitle('Settings');
  const {theme, setTheme} = useTheme();

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Settings</h1>
        <p className="text-muted-foreground">
          Manage your system preferences and store settings.
        </p>
      </div>

      {/* Theme Settings Card */}
      <Card>
        <CardHeader>
          <CardTitle>Appearance</CardTitle>
          <CardDescription>
            Customize how the admin dashboard looks.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <p className="text-sm font-medium">Theme</p>
            <div className="flex flex-wrap gap-2">
              <Button
                variant={theme === 'light' ? 'default' : 'outline'}
                onClick={() => setTheme('light')}
                className="flex items-center"
              >
                <Sun className="size-4"/>
                Light
              </Button>
              <Button
                variant={theme === 'dark' ? 'default' : 'outline'}
                onClick={() => setTheme('dark')}
                className="flex items-center"
              >
                <Moon className="size-4"/>
                Dark
              </Button>
              <Button
                variant={theme === 'system' ? 'default' : 'outline'}
                onClick={() => setTheme('system')}
                className="flex items-center"
              >
                <Monitor className="size-4"/>
                System
              </Button>
            </div>
            <p className="text-xs text-muted-foreground mt-2">
              Select your preferred appearance theme.
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
};

export default SettingsPage;
