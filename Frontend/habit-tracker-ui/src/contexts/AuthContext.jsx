import React, { createContext, useState, useContext, useEffect } from 'react';
import { jwtDecode } from 'jwt-decode';
import apiClient from '../services/api'; 

const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
    const [user, setUser] = useState(null);
    const [token, setToken] = useState(localStorage.getItem('authToken'));
    const [loading, setLoading] = useState(true); 

    useEffect(() => {
        const storedToken = localStorage.getItem('authToken');
        if (storedToken) {
            try {
                const decoded = jwtDecode(storedToken);
                const currentTime = Date.now() / 1000;
                if (decoded.exp > currentTime) {
                    setUser({ id: decoded.sub, email: decoded.email, username: decoded.name });
                    apiClient.defaults.headers.common['Authorization'] = `Bearer ${storedToken}`;
                    setToken(storedToken);
                } else {
        
                    localStorage.removeItem('authToken');
                    setUser(null);
                    setToken(null);
                    delete apiClient.defaults.headers.common['Authorization'];
                }
            } catch (e) {
                console.error("Invalid token during initial load", e);
                localStorage.removeItem('authToken');
                setUser(null);
                setToken(null);
                delete apiClient.defaults.headers.common['Authorization'];
            }
        }
        setLoading(false);
    }, []);

    const login = (newTokenData) => {

        localStorage.setItem('authToken', newTokenData.token);
        setToken(newTokenData.token);
        setUser(newTokenData.user);
        apiClient.defaults.headers.common['Authorization'] = `Bearer ${newTokenData.token}`;
    };

    const logout = () => {
        localStorage.removeItem('authToken');
        setToken(null);
        setUser(null);
        delete apiClient.defaults.headers.common['Authorization'];
    };

    return (
        <AuthContext.Provider value={{ user, token, login, logout, loading, isAuthenticated: !!user }}>
            {!loading && children}
        </AuthContext.Provider>
    );
};

export const useAuth = () => useContext(AuthContext);