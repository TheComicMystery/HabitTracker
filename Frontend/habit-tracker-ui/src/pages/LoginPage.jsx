import React, { useState } from 'react';
import { useNavigate, useLocation, Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { loginUser } from '../services/api';
import '../components/Auth/AuthForm.css';

const LoginPage = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);
    const { login } = useAuth();
    const navigate = useNavigate();
    const location = useLocation();
    const from = location.state?.from?.pathname || "/dashboard";

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');
        setLoading(true);
        try {
            const response = await loginUser({ email, password });
            login(response.data);
            navigate(from, { replace: true });
        } catch (err) {
            setError(err.response?.data?.message || 'Помилка входу. Перевірте дані.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="auth-split-screen">
            <div className="auth-left">
                <div className="auth-content">
                    <div className="auth-logo-wrapper">
                        <div className="auth-logo">HK</div>
                        <span className="auth-brand">HabitKnot</span>
                    </div>
                    
                    <h1 className="auth-title">З поверненням</h1>
                    <p className="auth-subtitle">Увійдіть, щоб продовжити свій прогрес.</p>
                    
                    {error && <div className="auth-error">{error}</div>}

                    <form onSubmit={handleSubmit} className="auth-form">
                        <div className="form-group">
                            <label htmlFor="email">Електронна пошта</label>
                            <input 
                                type="email" 
                                id="email" 
                                value={email} 
                                onChange={(e) => setEmail(e.target.value)} 
                                required 
                                placeholder="name@example.com"
                                autoComplete="email"
                            />
                        </div>
                        <div className="form-group">
                            <label htmlFor="password">Пароль</label>
                            <input 
                                type="password" 
                                id="password" 
                                value={password} 
                                onChange={(e) => setPassword(e.target.value)} 
                                required 
                                placeholder="••••••••"
                                autoComplete="current-password"
                            />
                        </div>
                        
                        <button type="submit" className="button button-primary full-width" disabled={loading}>
                            {loading ? 'Вхід...' : 'Увійти'}
                        </button>
                    </form>

                    <p className="auth-footer">
                        Немає акаунту? <Link to="/register">Зареєструватися</Link>
                    </p>
                </div>
            </div>
            
            <div className="auth-right">
                <div className="auth-journey-card glass">
                    <div className="journey-graphic">
                        <div className="journey-line"></div>
                        <div className="journey-point p1 completed"></div>
                        <div className="journey-point p2 completed"></div>
                        <div className="journey-point p3 active"></div>
                        <div className="journey-point p4"></div>
                    </div>
                    <h3>Сила в постійності</h3>
                    <p>Не переривай ланцюжок. Кожен день має значення для твого майбутнього успіху.</p>
                </div>
                
                <div className="blob-auth blob-3"></div>
            </div>
        </div>
    );
};

export default LoginPage;