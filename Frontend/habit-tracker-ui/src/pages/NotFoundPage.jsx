import React from 'react';
import { Link } from 'react-router-dom';

const NotFoundPage = () => {
    return (
        <div className="not-found-container">
            <style>{`
                .not-found-container {
                    min-height: 80vh;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    text-align: center;
                    padding: 20px;
                    background-color: var(--bg-primary);
                    color: var(--text-primary);
                }
                .not-found-content {
                    max-width: 500px;
                    animation: fadeIn 0.8s ease-out;
                }
                .ghost-illustration {
                    color: var(--color-primary);
                    margin-bottom: 20px;
                    animation: floatGhost 3s ease-in-out infinite;
                    filter: drop-shadow(0 10px 20px rgba(99, 102, 241, 0.3));
                }
                .error-code {
                    font-size: 6rem;
                    font-weight: 900;
                    line-height: 1;
                    margin: 0;
                    background: linear-gradient(135deg, var(--color-primary), var(--color-accent));
                    -webkit-background-clip: text;
                    -webkit-text-fill-color: transparent;
                }
                .error-title {
                    font-size: 1.5rem;
                    margin-top: 10px;
                    margin-bottom: 15px;
                    color: var(--text-primary);
                    font-weight: 700;
                }
                .error-desc {
                    color: var(--text-secondary);
                    margin-bottom: 30px;
                    line-height: 1.6;
                    font-size: 1.1rem;
                }
                .home-btn {
                    padding: 12px 32px;
                    font-size: 1.1rem;
                    border-radius: 50px;
                    text-decoration: none;
                    display: inline-block;
                    background: var(--color-primary);
                    color: white;
                    font-weight: 600;
                    transition: transform 0.2s, box-shadow 0.2s;
                }
                .home-btn:hover {
                    transform: translateY(-2px);
                    box-shadow: 0 5px 15px rgba(99, 102, 241, 0.4);
                }
                @keyframes floatGhost {
                    0%, 100% { transform: translateY(0); }
                    50% { transform: translateY(-15px); }
                }
                @keyframes fadeIn {
                    from { opacity: 0; transform: translateY(20px); }
                    to { opacity: 1; transform: translateY(0); }
                }
            `}</style>

            <div className="not-found-content">
                <div className="ghost-illustration">
                    <svg width="150" height="150" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
                        <path d="M9 22v-2c0-1.1.9-2 2-2h2a2 2 0 0 1 2 2v2"></path>
                        <path d="M9 22v-2"></path>
                        <path d="M15 22v-2"></path>
                        <path d="M12 2v2"></path>
                        <path d="M12 2a9 9 0 0 0-9 9v7c0 1.1.9 2 2 2h1a2 2 0 0 0 2-2v-2"></path>
                        <path d="M12 2a9 9 0 0 1 9 9v7c0 1.1-.9 2-2 2h-1a2 2 0 0 1-2-2v-2"></path>
                        <circle cx="9.5" cy="11.5" r="1.5" fill="currentColor" stroke="none"></circle>
                        <circle cx="14.5" cy="11.5" r="1.5" fill="currentColor" stroke="none"></circle>
                        <path d="M10 16c.5.5 1.5.5 2 0"></path>
                    </svg>
                </div>
                
                <h1 className="error-code">404</h1>
                <h2 className="error-title">Упс! Здається, ми заблукали</h2>
                <p className="error-desc">
                    Ця сторінка зникла, наче невиконана звичка.<br/>
                    Повертайся назад, щоб продовжити свій шлях.
                </p>
                
                <Link to="/" className="home-btn">
                    На головну
                </Link>
            </div>
        </div>
    );
};

export default NotFoundPage;