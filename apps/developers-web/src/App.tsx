import { BrowserRouter as Router, Routes, Route, Navigate, Link, useNavigate } from 'react-router-dom';
import { useState } from 'react';
import { Toaster } from 'sonner';

import Login from './pages/Login';
import Register from './pages/Register';
import ForgotPassword from './pages/ForgotPassword';
import ResetPassword from './pages/ResetPassword';
import Dashboard from './pages/Dashboard';
import { api } from './lib/api';
import ProfilePage from '@shared/components/session/ProfilePage';
import SessionGuard from '@shared/components/session/SessionGuard';

function DashboardRoute({ onLogout }: { onLogout: () => void }) {
  const navigate = useNavigate();
  return <Dashboard onLogout={onLogout} onProfile={() => navigate('/profile')} />;
}

function ProfileRoute() {
  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white px-6 py-4">
        <Link to="/" className="text-sm font-semibold text-slate-500 hover:text-primary">← Volver al portal</Link>
      </header>
      <ProfilePage api={api} basePath="/developer/auth" tokenKey="fel_developer_auth" />
    </div>
  );
}

function App() {
  const [isAuthenticated, setIsAuthenticated] = useState(
    !!localStorage.getItem('fel_developer_auth')
  );

  const handleLogout = () => {
    localStorage.removeItem('fel_developer_auth');
    localStorage.removeItem('fel_developer_id');
    localStorage.removeItem('fel_developer_name');
    setIsAuthenticated(false);
  };

  return (
    <Router>
      <Toaster position="top-right" richColors />
      {/* Un solo vigilante por encima de las rutas: al navegar no pierde la actividad ni el estado de la renovación. */}
      {isAuthenticated && <SessionGuard api={api} tokenKey="fel_developer_auth" basePath="/developer/auth" onExpired={handleLogout} />}
      <Routes>
        <Route
          path="/login"
          element={isAuthenticated ? <Navigate to="/" /> : <Login onAuthSuccess={() => setIsAuthenticated(true)} />}
        />
        <Route
          path="/register"
          element={isAuthenticated ? <Navigate to="/" /> : <Register />}
        />
        <Route path="/forgot-password" element={<ForgotPassword />} />
        <Route path="/reset-password" element={<ResetPassword />} />
        <Route
          path="/"
          element={isAuthenticated ? <DashboardRoute onLogout={handleLogout} /> : <Navigate to="/login" />}
        />
        <Route
          path="/profile"
          element={isAuthenticated ? <ProfileRoute /> : <Navigate to="/login" />}
        />
      </Routes>
    </Router>
  );
}

export default App;
