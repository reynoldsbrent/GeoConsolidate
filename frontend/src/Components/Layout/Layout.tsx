import React, { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

interface LayoutProps {
  children: React.ReactNode;
}

const Layout: React.FC<LayoutProps> = ({ children }) => {
  const location = useLocation();

  useEffect(() => {
    // Reset scroll position when route changes
    window.scrollTo(0, 0);
  }, [location.pathname]);

  return (
    <div className="min-h-screen relative">
      <div className="pt-16"> 
        {children}
      </div>
    </div>
  );
};

export default Layout;