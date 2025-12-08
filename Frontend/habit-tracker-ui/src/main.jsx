import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './assets/styles/global.css'; 
import { AuthProvider } from './contexts/AuthContext';
import { ThemeProvider } from './contexts/ThemeContext';
import { MobileProvider } from './contexts/MobileContext';
import { BrowserRouter } from 'react-router-dom';

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <BrowserRouter>
      <ThemeProvider>
        <MobileProvider>
          <AuthProvider>
            <App />
          </AuthProvider>
        </MobileProvider>
      </ThemeProvider>
    </BrowserRouter>
  </React.StrictMode>
);