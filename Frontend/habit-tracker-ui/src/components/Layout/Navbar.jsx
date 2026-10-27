import React from 'react';
import { Link, NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { useTheme } from '../../contexts/ThemeContext';
import { useMobile } from '../../contexts/MobileContext';
import { ChartIcon, GridIcon } from '../UI/Icons';
import './Navbar.css';

const Navbar = () => {
    const { isAuthenticated, user, logout } = useAuth();
    const { isDarkMode, toggleTheme } = useTheme();
    const { isMobileView, toggleMobileView } = useMobile();
    const navigate = useNavigate();

    const handleLogout = () => {
        logout();
        navigate('/dashboard');
    };

    return (
        <nav className="navbar">
            <div className="container navbar-container">
                <div className="navbar-left">
                    <Link to="/dashboard" className="logo">
                        <div className="logo-icon">HK</div>
                        <span className="logo-text">HabitKnot</span>
                    </Link>
                </div>

                <div className="navbar-right">
                    {isAuthenticated && (
                        <div className="nav-links">
                            <NavLink to="/dashboard" className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
                                <ChartIcon />
                                <span>Огляд</span>
                            </NavLink>
                            <NavLink to="/habits" className={({ isActive }) => `nav-link ${isActive ? 'active' : ''}`}>
                                <GridIcon />
                                <span>Звички</span>
                            </NavLink>
                        </div>
                    )}

                    <div className="actions-divider"></div>

                    <div className="toggles-wrapper">
                        <button
                            onClick={toggleMobileView}
                            className={`icon-btn ${isMobileView ? 'active-mobile' : ''}`}
                            title="Мобільний вигляд"
                        >
                            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                <rect x="7" y="2" width="10" height="20" rx="2" ry="2"></rect>
                                <line x1="12" y1="18" x2="12.01" y2="18"></line>
                            </svg>
                        </button>

                        <button onClick={toggleTheme} className="icon-btn" title="Тема">
                            {isDarkMode ? (
                                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                    <circle cx="12" cy="12" r="5"></circle>
                                    <line x1="12" y1="1" x2="12" y2="3"></line>
                                    <line x1="12" y1="21" x2="12" y2="23"></line>
                                    <line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line>
                                    <line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line>
                                    <line x1="1" y1="12" x2="3" y2="12"></line>
                                    <line x1="21" y1="12" x2="23" y2="12"></line>
                                    <line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line>
                                    <line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line>
                                </svg>
                            ) : (
                                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                    <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path>
                                </svg>
                            )}
                        </button>
                    </div>

                    {isAuthenticated ? (
                        <div className="user-menu">
                            <div className="user-avatar">{user?.username?.[0]?.toUpperCase()}</div>
                            <button onClick={handleLogout} className="logout-btn">Вийти</button>
                        </div>
                    ) : (
                        <Link to="/register" className="button button-primary nav-cta">
                            {isMobileView ? 'Реєстр.' : 'Реєстрація'}
                        </Link>
                    )}
                </div>
            </div>
        </nav>
    );
};

export default Navbar;
