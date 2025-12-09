import React from 'react';
import {Outlet} from 'react-router-dom';
import Sidebar from '@/components/layout/Sidebar';
import Header from '@/components/layout/Header';

const MainLayout: React.FC = () => {
  return (
    <div className="flex h-screen overflow-hidden">
      {/* Sidebar */}
      <Sidebar/>

      {/* Main Content */}
      <div className="flex flex-col flex-1 overflow-hidden">
        {/* Header */}
        <Header/>

        {/* Page Content */}
        <main className="flex-1 overflow-auto p-6">
          <Outlet/>
        </main>
      </div>
    </div>
  );
};

export default MainLayout;
