import React, { useCallback, useEffect, useRef, useState } from 'react';
import { createPomodoroSession, getHabits, trackHabit } from '../../services/api';
import './FloatingPomodoro.css';

const MODES = {
  pomodoro: { time: 25 * 60, label: 'Фокус' },
  shortBreak: { time: 5 * 60, label: 'Пауза' },
  longBreak: { time: 15 * 60, label: 'Відпочинок' }
};

const FloatingPomodoro = () => {
  const [isOpen, setIsOpen] = useState(false);
  const [mode, setMode] = useState('pomodoro');
  const [timeLeft, setTimeLeft] = useState(MODES.pomodoro.time);
  const [initialTime, setInitialTime] = useState(MODES.pomodoro.time);
  const [isActive, setIsActive] = useState(false);
  const [habits, setHabits] = useState([]);
  const [selectedHabitId, setSelectedHabitId] = useState('');
  const [message, setMessage] = useState('');
  const timerRef = useRef(null);
  const startedAtRef = useRef(null);

  useEffect(() => {
    const fetchHabits = async () => {
      try {
        const response = await getHabits(false, false);
        setHabits(response.data || []);
      } catch (error) {
        console.error('Failed to load habits', error);
      }
    };
    fetchHabits();
  }, []);

  const finishTimer = useCallback(async () => {
    setIsActive(false);
    const endedAt = new Date();
    const startedAt = startedAtRef.current || new Date(endedAt.getTime() - initialTime * 1000);
    const durationMinutes = Math.max(1, Math.round((endedAt - startedAt) / 60000));

    try {
      await createPomodoroSession({
        habitId: selectedHabitId || null,
        startedAt,
        endedAt,
        durationMinutes,
        mode,
        completed: true
      });
    } catch {
    }

    if (selectedHabitId && mode === 'pomodoro') {
      try {
        await trackHabit(selectedHabitId, { date: new Date(), completedCount: 1 });
        setMessage('Фокус завершено, прогрес зараховано.');
      } catch {
        setMessage('Фокус завершено, але звичку не оновлено.');
      }
    } else {
      setMessage('Час вийшов.');
    }
    startedAtRef.current = null;
    setTimeout(() => setMessage(''), 4500);
  }, [selectedHabitId, mode, initialTime]);

  useEffect(() => {
    if (isActive && timeLeft > 0) {
      timerRef.current = setInterval(() => setTimeLeft(prev => prev - 1), 1000);
    } else if (timeLeft === 0 && isActive) {
      clearInterval(timerRef.current);
      finishTimer();
    }
    return () => clearInterval(timerRef.current);
  }, [isActive, timeLeft, finishTimer]);

  const changeMode = (nextMode) => {
    setMode(nextMode);
    setIsActive(false);
    startedAtRef.current = null;
    setTimeLeft(MODES[nextMode].time);
    setInitialTime(MODES[nextMode].time);
    setMessage('');
  };

  const resetTimer = () => {
    setIsActive(false);
    startedAtRef.current = null;
    setTimeLeft(initialTime);
    setMessage('');
  };

  const toggleTimer = () => {
    setIsActive(prev => {
      if (!prev && !startedAtRef.current) startedAtRef.current = new Date();
      return !prev;
    });
  };

  const formatTime = (seconds) => {
    const minutes = Math.floor(seconds / 60);
    const rest = seconds % 60;
    return `${minutes < 10 ? '0' : ''}${minutes}:${rest < 10 ? '0' : ''}${rest}`;
  };

  const progress = 100 - (timeLeft / initialTime) * 100;

  return (
    <div className={`floating-pomodoro ${isOpen ? 'open' : ''}`}>
      {isOpen && (
        <div className="floating-pomodoro-panel">
          <div className="floating-pomodoro-header">
            <strong>Помодоро</strong>
            <button type="button" onClick={() => setIsOpen(false)} aria-label="Закрити">×</button>
          </div>

          <div className="floating-mode-tabs">
            {Object.keys(MODES).map(key => (
              <button
                key={key}
                type="button"
                className={mode === key ? 'active' : ''}
                onClick={() => changeMode(key)}
              >
                {MODES[key].label}
              </button>
            ))}
          </div>

          <select value={selectedHabitId} onChange={(e) => setSelectedHabitId(e.target.value)} disabled={isActive}>
            <option value="">Без прив'язки до звички</option>
            {habits.map(habit => (
              <option key={habit.id} value={habit.id}>{habit.name}</option>
            ))}
          </select>

          <div className="floating-timer-ring" style={{ '--progress': `${progress}%` }}>
            <div>
              <span>{formatTime(timeLeft)}</span>
              <small>{MODES[mode].label}</small>
            </div>
          </div>

          <div className="floating-timer-actions">
            <button type="button" className="button button-primary" onClick={toggleTimer}>
              {isActive ? 'Пауза' : 'Старт'}
            </button>
            <button type="button" className="button button-secondary" onClick={resetTimer}>
              Скинути
            </button>
          </div>

          {message && <div className="floating-pomodoro-message">{message}</div>}
        </div>
      )}

      <button
        type="button"
        className="floating-pomodoro-button"
        onClick={() => setIsOpen(prev => !prev)}
        aria-label="Відкрити помодоро"
      >
        <span>25</span>
      </button>
    </div>
  );
};

export default FloatingPomodoro;
