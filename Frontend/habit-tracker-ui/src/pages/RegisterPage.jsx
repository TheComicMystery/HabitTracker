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

    const getErrorMessage = (err) => {
        if (err.response?.data?.message) return err.response.data.message;
        if (err.code === 'ERR_NETWORK' || !err.response) {
            return 'Не вдалося підключитися до API. Перевірте, чи запущений backend.';
        }
        return 'Помилка реєстрації.';
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (formData.password !== formData.confirmPassword) {
            setError('Паролі не збігаються.');
            return;
        }
        if (formData.password.length < 8) {
            setError('Пароль має містити щонайменше 8 символів.');
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
            setError(getErrorMessage(err));
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
                    <p className="auth-subtitle">Приєднуйтеся до HabitKnot і будуйте звички у власному темпі.</p>

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
                                    minLength={8}
                                    placeholder="********"
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
                                    minLength={8}
                                    placeholder="********"
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
                    <h3>Почніть свій шлях</h3>
                    <p>Відстежуйте прогрес, аналізуйте результати та відзначайте маленькі перемоги щодня.</p>

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
