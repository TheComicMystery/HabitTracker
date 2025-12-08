import React, { useState, useEffect, useRef, useCallback } from 'react';
import { getHabits, trackHabit } from '../services/api';
import './PomodoroPage.css';

const TICK_SOUND = 'https://www.soundjay.com/button/sounds/button-17.mp3'; 
const ALARM_SOUND = 'https://www.soundjay.com/misc/sounds/magic-chime-01.mp3'; 

const MODES = {
    pomodoro: { time: 25 * 60, label: 'Фокус 🔥', color: '#6366f1' },
    shortBreak: { time: 5 * 60, label: 'Пауза ☕', color: '#10b981' },
    longBreak: { time: 15 * 60, label: 'Відпочинок 🌴', color: '#3b82f6' }
};

const PomodoroPage = () => {
    const [mode, setMode] = useState('pomodoro');
    const [timeLeft, setTimeLeft] = useState(MODES.pomodoro.time);
    const [initialTime, setInitialTime] = useState(MODES.pomodoro.time);
    const [isActive, setIsActive] = useState(false);
    const [habits, setHabits] = useState([]);
    const [selectedHabitId, setSelectedHabitId] = useState('');
    const [completionMessage, setCompletionMessage] = useState('');
    const [isMuted, setIsMuted] = useState(false);
    
    const timerRef = useRef(null);
    const tickAudioRef = useRef(new Audio(TICK_SOUND));
    const alarmAudioRef = useRef(new Audio(ALARM_SOUND));

    useEffect(() => {
        tickAudioRef.current.volume = 0.3; 
        alarmAudioRef.current.volume = 0.6;

        const fetchHabits = async () => {
            try {
                const response = await getHabits(false);
                setHabits(response.data || []);
            } catch (error) {
                console.error("Failed to load habits", error);
            }
        };
        fetchHabits();
    }, []);

    const handleTimerFinish = useCallback(async () => {
        setIsActive(false);
        alarmAudioRef.current.currentTime = 0;
        alarmAudioRef.current.play().catch(e => console.log("Audio play error:", e));

        if (selectedHabitId && mode === 'pomodoro') {
            try {
                const habit = habits.find(h => h.id === selectedHabitId);
                await trackHabit(selectedHabitId, { 
                    date: new Date(), 
                    completedCount: 1 
                });
                setCompletionMessage(`🎉 Супер! Прогресс по "${habit.name}" зараховано!`);
            } catch (err) {
                setCompletionMessage('⚠️ Таймер завершено, але не вдалося оновити звичку.');
            }
        } else {
            setCompletionMessage('⏰ Час вийшов!');
        }

        setTimeout(() => setCompletionMessage(''), 5000);
    }, [selectedHabitId, mode, habits]);

    useEffect(() => {
        if (isActive && timeLeft > 0) {
            timerRef.current = setInterval(() => {
                setTimeLeft((prev) => prev - 1);
                
                if (!isMuted) {
                    const sound = tickAudioRef.current;
                    sound.currentTime = 0;
                    const playPromise = sound.play();
                    if (playPromise !== undefined) {
                        playPromise.catch(() => {});
                    }
                }
            }, 1000);
        } else if (timeLeft === 0 && isActive) {
            clearInterval(timerRef.current);
            handleTimerFinish();
        }
        return () => clearInterval(timerRef.current);
    }, [isActive, timeLeft, handleTimerFinish, isMuted]);

    const toggleTimer = () => setIsActive(!isActive);

    const resetTimer = () => {
        setIsActive(false);
        setTimeLeft(initialTime);
        setCompletionMessage('');
    };

    const changeMode = (newMode) => {
        setMode(newMode);
        setIsActive(false);
        const newTime = MODES[newMode].time;
        setTimeLeft(newTime);
        setInitialTime(newTime);
        setCompletionMessage('');
    };

    const adjustTime = (minutes) => {
        const stepSeconds = 5 * 60; 
        let newTime;

        if (minutes > 0) {
            if (timeLeft < stepSeconds) {
                newTime = stepSeconds;
            } else {
                newTime = timeLeft + (minutes * 60);
            }
        } else {
            newTime = timeLeft + (minutes * 60);
        }

        newTime = Math.max(60, newTime); 
        
        setTimeLeft(newTime);
        setInitialTime(newTime);
    };

    const formatTime = (seconds) => {
        const m = Math.floor(seconds / 60);
        const s = seconds % 60;
        return `${m < 10 ? '0' : ''}${m}:${s < 10 ? '0' : ''}${s}`;
    };

    const progress = 100 - (timeLeft / initialTime) * 100;
    const currentColor = MODES[mode].color;

    return (
        <div className="pomodoro-container" style={{ '--theme-color': currentColor }}>
            <div className="glow-bg"></div>

            <div className="timer-wrapper glass-panel">
                <div className="sound-toggle-wrapper">
                    <button 
                        onClick={() => setIsMuted(!isMuted)} 
                        className={`sound-btn ${isMuted ? 'muted' : ''}`}
                        title={isMuted ? "Увімкнути цокання" : "Вимкнути цокання"}
                    >
                        {isMuted ? '🔇' : '🔊'}
                    </button>
                </div>

                <div className="mode-tabs">
                    {Object.keys(MODES).map((m) => (
                        <button
                            key={m}
                            className={`mode-tab ${mode === m ? 'active' : ''}`}
                            onClick={() => changeMode(m)}
                        >
                            {MODES[m].label}
                        </button>
                    ))}
                </div>

                <div className="habit-selector-wrapper">
                    <label>Над чим працюємо?</label>
                    <select 
                        value={selectedHabitId} 
                        onChange={(e) => setSelectedHabitId(e.target.value)}
                        className="habit-select"
                        disabled={isActive}
                    >
                        <option value="">🎯 Просто фокус (без прив'язки)</option>
                        {habits.map(h => (
                            <option key={h.id} value={h.id}>{h.name}</option>
                        ))}
                    </select>
                </div>

                <div className="timer-circle-container">
                    <svg className="timer-svg" viewBox="0 0 100 100">
                        <circle className="timer-circle-bg" cx="50" cy="50" r="45" />
                        <circle 
                            className="timer-circle-progress" 
                            cx="50" cy="50" r="45"
                            strokeDasharray="283"
                            strokeDashoffset={283 - (283 * progress / 100)}
                        />
                    </svg>
                    
                    <div className="timer-content">
                        <div className="timer-text">
                            {formatTime(timeLeft)}
                        </div>
                        
                        {!isActive && (
                            <div className="time-adjust-controls">
                                <button onClick={() => adjustTime(-5)} className="adjust-btn" title="-5 хв">-5</button>
                                <button onClick={() => adjustTime(5)} className="adjust-btn" title="+5 хв">+5</button>
                            </div>
                        )}
                    </div>
                </div>

                <div className="timer-controls">
                    <button 
                        className={`control-btn main ${isActive ? 'pause' : 'start'}`}
                        onClick={toggleTimer}
                    >
                        {isActive ? 'ПАУЗА' : 'СТАРТ'}
                    </button>
                    <button className="control-btn reset" onClick={resetTimer}>
                        ↺
                    </button>
                </div>

                {completionMessage && (
                    <div className="completion-toast show">
                        {completionMessage}
                    </div>
                )}
            </div>
        </div>
    );
};

export default PomodoroPage;