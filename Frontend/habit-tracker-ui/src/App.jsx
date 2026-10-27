import React, { Suspense, lazy } from 'react';
import { Routes, Route, Navigate, useLocation } from 'react-router-dom';
import Navbar from './components/Layout/Navbar';
import ProtectedRoute from './components/Auth/ProtectedRoute';
import FloatingPomodoro from './components/Pomodoro/FloatingPomodoro';
import SmartOnboarding from './components/Onboarding/SmartOnboarding';
import { useMobile } from './contexts/MobileContext';
import { useAuth } from './contexts/AuthContext';
import './App.css';

const LoginPage = lazy(() => import('./pages/LoginPage'));
const RegisterPage = lazy(() => import('./pages/RegisterPage'));
const DashboardPage = lazy(() => import('./pages/DashboardPage'));
const HabitsPage = lazy(() => import('./pages/HabitsPage'));
const NotFoundPage = lazy(() => import('./pages/NotFoundPage'));

function App() {
  const { isMobileView } = useMobile();
  const auth = useAuth();
  const isAuthenticated = Boolean(auth?.isAuthenticated);
  const location = useLocation();
  const hideFloatingPomodoro = ['/login', '/register'].includes(location.pathname);

  return (
    <div className={`app-container ${isMobileView ? 'mobile-simulation' : ''}`}>
      <Navbar />
      <main className="main-content container">
        <Suspense fallback={<div className="app-loading">Завантаження...</div>}>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/dashboard" element={<DashboardPage />} />
            <Route
              path="/habits"
              element={
                <ProtectedRoute>
                  <HabitsPage />
                </ProtectedRoute>
              }
            />
            <Route
              path="/calendar"
              element={
                <ProtectedRoute>
                  <Navigate to="/habits" replace />
                </ProtectedRoute>
              }
            />
            <Route
              path="/pomodoro"
              element={
                <ProtectedRoute>
                  <Navigate to="/habits" replace />
                </ProtectedRoute>
              }
            />
            <Route path="/" element={<Navigate to="/dashboard" replace />} />
            <Route path="*" element={<NotFoundPage />} />
          </Routes>
        </Suspense>
      </main>
      {isAuthenticated && !hideFloatingPomodoro && <FloatingPomodoro />}
      {isAuthenticated && !hideFloatingPomodoro && <SmartOnboarding isAuthenticated={isAuthenticated} />}
    </div>
  );
}

export default App;
