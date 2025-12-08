import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import Navbar from './components/Layout/Navbar';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import DashboardPage from './pages/DashboardPage';
import HabitsPage from './pages/HabitsPage';
import CalendarPage from './pages/CalendarPage';
import PomodoroPage from './pages/PomodoroPage';
import NotFoundPage from './pages/NotFoundPage';
import ProtectedRoute from './components/Auth/ProtectedRoute';
import { useMobile } from './contexts/MobileContext';
import './App.css';

function App() {
  const { isMobileView } = useMobile();

  return (
    <div className={`app-container ${isMobileView ? 'mobile-simulation' : ''}`}>
      <Navbar />
      <main className="main-content container">
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
                <CalendarPage />
              </ProtectedRoute>
            } 
          />
          <Route 
            path="/pomodoro" 
            element={
              <ProtectedRoute>
                <PomodoroPage />
              </ProtectedRoute>
            } 
          />
          <Route path="/" element={<Navigate to="/dashboard" replace />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;