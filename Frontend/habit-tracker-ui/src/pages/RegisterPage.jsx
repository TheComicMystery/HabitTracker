import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { registerUser } from '../services/api';
import '../components/Auth/AuthForm.css';

const RegisterPage = () => {
    const [formData, setFormData] = useState({
        username: '',
        email: '',
        password: '',
        confirmPassword: ''
    });
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);
    const navigate = useNavigate();

    const handleChange = (e) => {
        setFormData({ ...formData, [e.target.name]: e.target.value });
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (formData.password !== formData.confirmPassword) {
            setError('Паролі не співпадають!');
            return;
        }
        setError('');
        setLoading(true);
        try {
            await registerUser({ 
                username: formData.username, 
                email: formData.email, 
                password: formData.password 
            });
            navigate('/login'); 
        } catch (err) {
            setError(err.response?.data?.message || 'Помилка реєстрації.');
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
                    
                    <h1 className="auth-title">Створення акаунту</h1>
                    <p className="auth-subtitle">Приєднуйтесь до спільноти та змінюйте своє життя.</p>
                    
                    {error && <div className="auth-error">{error}</div>}

                    <form onSubmit={handleSubmit} className="auth-form">
                        <div className="form-group">
                            <label>Ім'я користувача</label>
                            <input 
                                type="text" 
                                name="username"
                                value={formData.username} 
                                onChange={handleChange} 
                                required 
                                placeholder="Наприклад: Alex"
                            />
                        </div>
                        <div className="form-group">
                            <label>Електронна пошта</label>
                            <input 
                                type="email" 
                                name="email"
                                value={formData.email} 
                                onChange={handleChange} 
                                required 
                                placeholder="name@example.com"
                            />
                        </div>
                        <div className="form-row">
                            <div className="form-group">
                                <label>Пароль</label>
                                <input 
                                    type="password" 
                                    name="password"
                                    value={formData.password} 
                                    onChange={handleChange} 
                                    required 
                                    placeholder="••••••••"
                                />
                            </div>
                            <div className="form-group">
                                <label>Підтвердження</label>
                                <input 
                                    type="password" 
                                    name="confirmPassword"
                                    value={formData.confirmPassword} 
                                    onChange={handleChange} 
                                    required 
                                    placeholder="••••••••"
                                />
                            </div>
                        </div>
                        
                        <button type="submit" className="button button-primary full-width" disabled={loading}>
                            {loading ? 'Створення...' : 'Зареєструватися'}
                        </button>
                    </form>

                    <p className="auth-footer">
                        Вже маєте акаунт? <Link to="/login">Увійти</Link>
                    </p>
                </div>
            </div>
            
            <div className="auth-right">
                <div className="auth-feature-card glass">
                    <div className="feature-icon">🚀</div>
                    <h3>Почни свій шлях</h3>
                    <p>Відстежуй прогрес, аналізуй результати та святкуй маленькі перемоги щодня.</p>
                    
                    <div className="mini-stats">
                        <div className="stat-pill">📈 Прогрес</div>
                        <div className="stat-pill">🎯 Цілі</div>
                        <div className="stat-pill">🔥 Стріки</div>
                    </div>
                </div>
                
                <div className="blob-auth blob-1"></div>
                <div className="blob-auth blob-2"></div>
            </div>
        </div>
    );
};

export default RegisterPage;