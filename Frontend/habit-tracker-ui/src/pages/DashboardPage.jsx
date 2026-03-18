import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { getHabits, getHabitEntries } from '../services/api';
import AnalyticsWidgets from '../components/Dashboard/AnalyticsWidgets';
import Modal from '../components/UI/Modal';
import './DashboardPage.css';

const DashboardPage = () => {
  const { isAuthenticated, user } = useAuth();
  const [habits, setHabits] = useState([]);
  const [allEntries, setAllEntries] = useState([]);
  const [loading, setLoading] = useState(true);
  
  const [selectedDate, setSelectedDate] = useState(null);
  const [dayDetails, setDayDetails] = useState([]);

  useEffect(() => {
    if (isAuthenticated) {
        const loadData = async () => {
            try {
                const habitsRes = await getHabits(true);
                const habitsData = habitsRes.data || [];
                setHabits(habitsData);

                const endDate = new Date().toISOString();
                const startDate = new Date(new Date().setMonth(new Date().getMonth() - 6)).toISOString();
                
                const entryPromises = habitsData.map(h => 
                    getHabitEntries(h.id, startDate, endDate)
                        .then(res => res.data)
                        .catch(() => [])
                );
                
                const results = await Promise.all(entryPromises);
                setAllEntries(results.flat());
            } catch (e) {
                console.error(e);
            } finally {
                setLoading(false);
            }
        };
        loadData();
    }
  }, [isAuthenticated]);

  const handleHeatmapClick = (date) => {
      const dateStr = date.toISOString().split('T')[0];
      const dayOfWeek = date.getDay();

      const activeForDay = habits.filter(h => {
          const habitStart = new Date(h.startDate); habitStart.setHours(0,0,0,0);
          const currentDay = new Date(date); currentDay.setHours(0,0,0,0);
          return h.activeDays.includes(dayOfWeek) && habitStart <= currentDay;
      });

      const details = activeForDay.map(habit => {
          const entry = allEntries.find(e => 
              e.habitId === habit.id && 
              e.date.startsWith(dateStr)
          );
          return {
              name: habit.name,
              target: habit.targetCount,
              completed: entry ? entry.completedCount : 0,
              icon: habit.icon,
              color: habit.color
          };
      });

      setDayDetails(details);
      setSelectedDate(date);
  };

  const getGreeting = () => {
    const hour = new Date().getHours();
    if (hour < 12) return 'Добрий ранок';
    if (hour < 18) return 'Добрий день';
    return 'Добрий вечір';
  };

  return (
    <div className="dashboard-container">
      <div className="ambient-light"></div>
      
      {isAuthenticated ? (
        <div className="bento-grid container">
          <div className="bento-item header-card">
            <div className="header-content">
                <h1>{getGreeting()}, <span className="highlight">{user?.username}</span>! 👋</h1>
                <p>Твій прогрес сьогодні виглядає чудово. Продовжуй у тому ж дусі!</p>
                <div className="action-buttons">
                    <Link to="/habits" className="button button-primary">Трекати звичку</Link>
                    <Link to="/pomodoro" className="button button-secondary">Таймер</Link>
                </div>
            </div>
            <div className="header-decoration">🔥</div>
          </div>

          {!loading && (
            <AnalyticsWidgets 
                habits={habits} 
                entries={allEntries} 
                onDateClick={handleHeatmapClick}
            />
          )}

          <div className="bento-item quote-card" style={{gridColumn: 'span 4'}}>
            <div className="quote-icon">❝</div>
            <div className="quote-content-wrapper">
                <p className="quote-text">«Успіх — це сума невеликих зусиль, що повторюються день у день»</p>
                <span className="quote-author">Роберт Кольєр</span>
            </div>
          </div>
        </div>
      ) : (
        <div className="landing-wrapper container">
            <div className="landing-hero">
                <span className="pill-badge">🚀 Версія 2.0</span>
                <h1 className="hero-title">Будуй майбутнє,<br/>одна звичка за раз.</h1>
                <p className="hero-desc">HabitKnot допомагає тобі організувати життя, досягати цілей та ставати кращою версією себе за допомогою наукового підходу.</p>
                <div className="landing-buttons">
                    <Link to="/register" className="button button-primary button-xl">Почати безкоштовно</Link>
                    <Link to="/login" className="button button-secondary button-xl">Увійти</Link>
                </div>
            </div>
            
            <div className="landing-visuals">
                <div className="visual-card card-water">
                    <div className="feature-icon-placeholder water-icon">💧</div>
                    <div className="card-text">
                        <strong>Пити воду</strong>
                        <span>8 склянок • Виконано</span>
                    </div>
                </div>
                <div className="visual-card card-read">
                    <div className="feature-icon-placeholder read-icon">📚</div>
                    <div className="card-text">
                        <strong>Читати 30 хв</strong>
                        <span>В процесі • 10/30</span>
                    </div>
                </div>
            </div>
        </div>
      )}

      {selectedDate && (
          <Modal onClose={() => setSelectedDate(null)} title={`Історія за ${selectedDate.toLocaleDateString('uk-UA')}`}>
              <div className="day-details-list">
                  {dayDetails.length > 0 ? (
                      dayDetails.map((detail, idx) => (
                          <div key={idx} className="day-detail-item" style={{display: 'flex', justifyContent: 'space-between', padding: '12px 0', borderBottom: '1px solid var(--border-color)', alignItems: 'center'}}>
                              <div style={{display: 'flex', alignItems: 'center', gap: '12px'}}>
                                  <span style={{fontSize: '1.5rem', width: '30px', textAlign: 'center'}}>{detail.icon || '📌'}</span>
                                  <span style={{fontWeight: '600', fontSize: '1rem'}}>{detail.name}</span>
                              </div>
                              <div style={{
                                  padding: '6px 12px', 
                                  borderRadius: '8px', 
                                  backgroundColor: detail.completed >= detail.target ? 'rgba(16, 185, 129, 0.15)' : 'var(--bg-hover)',
                                  color: detail.completed >= detail.target ? 'var(--color-accent)' : 'var(--text-secondary)',
                                  fontWeight: '700',
                                  fontSize: '0.9rem'
                              }}>
                                  {detail.completed} / {detail.target}
                              </div>
                          </div>
                      ))
                  ) : (
                      <p style={{textAlign: 'center', color: 'var(--text-secondary)', padding: '20px'}}>Немає запланованих звичок для цього дня.</p>
                  )}
              </div>
          </Modal>
      )}
    </div>
  );
};

export default DashboardPage;