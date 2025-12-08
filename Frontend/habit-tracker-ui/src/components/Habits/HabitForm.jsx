import React, { useState, useEffect } from 'react';
import './HabitForm.css';

const PREDEFINED_COLORS = [
    '#ef4444', '#f97316', '#f59e0b', '#84cc16', '#10b981', 
    '#06b6d4', '#3b82f6', '#6366f1', '#8b5cf6', '#d946ef', '#f43f5e'
];

const daysOfWeek = [
    { label: "Пн", value: 1 },
    { label: "Вт", value: 2 },
    { label: "Ср", value: 3 },
    { label: "Чт", value: 4 },
    { label: "Пт", value: 5 },
    { label: "Сб", value: 6 },
    { label: "Нд", value: 0 }
];

const HabitForm = ({ onSubmit, initialData = null, onCancel, isLoading }) => {
    const [name, setName] = useState('');
    const [description, setDescription] = useState('');
    const [color, setColor] = useState('#6366f1');
    const [targetCount, setTargetCount] = useState(1);
    const [activeDays, setActiveDays] = useState([1, 2, 3, 4, 5, 6, 0]);

    useEffect(() => {
        if (initialData) {
            setName(initialData.name || '');
            setDescription(initialData.description || '');
            setColor(initialData.color || '#6366f1');
            setTargetCount(initialData.targetCount || 1);
            setActiveDays(initialData.activeDays || [1, 2, 3, 4, 5, 6, 0]);
        }
    }, [initialData]);

    const handleDayToggle = (dayValue) => {
        setActiveDays(prev => 
            prev.includes(dayValue) ? prev.filter(d => d !== dayValue) : [...prev, dayValue]
        );
    };

    const handleSubmit = (e) => {
        e.preventDefault();
        onSubmit({ name, description, color, targetCount: Number(targetCount), activeDays });
    };

    return (
        <form onSubmit={handleSubmit} className="habit-form-container">
            <div className="form-group">
                <label>Назва звички</label>
                <input 
                    type="text" 
                    value={name} 
                    onChange={e => setName(e.target.value)} 
                    placeholder="Напр. Читати 15 хвилин"
                    required 
                    autoFocus
                />
            </div>

            <div className="form-group">
                <label>Опис (опціонально)</label>
                <textarea 
                    value={description} 
                    onChange={e => setDescription(e.target.value)} 
                    placeholder="Чому це важливо для мене?"
                    rows="2"
                />
            </div>

            <div className="form-row-split">
                <div className="form-group half">
                    <label>Ціль (разів на день)</label>
                    <div className="counter-input">
                        <button type="button" onClick={() => setTargetCount(Math.max(1, targetCount - 1))}>-</button>
                        <input 
                            type="number" 
                            value={targetCount} 
                            onChange={e => setTargetCount(e.target.value)}
                            min="1" 
                        />
                        <button type="button" onClick={() => setTargetCount(Number(targetCount) + 1)}>+</button>
                    </div>
                </div>
            </div>

            <div className="form-group">
                <label>Колір</label>
                <div className="color-picker-grid">
                    {PREDEFINED_COLORS.map(c => (
                        <div 
                            key={c} 
                            className={`color-swatch ${color === c ? 'selected' : ''}`}
                            style={{ backgroundColor: c }}
                            onClick={() => setColor(c)}
                        />
                    ))}
                </div>
            </div>

            <div className="form-group">
                <label>Дні повторення</label>
                <div className="days-grid">
                    {daysOfWeek.map(day => (
                        <button
                            type="button"
                            key={day.value}
                            className={`day-toggle ${activeDays.includes(day.value) ? 'active' : ''}`}
                            onClick={() => handleDayToggle(day.value)}
                        >
                            {day.label}
                        </button>
                    ))}
                </div>
            </div>

            <div className="form-actions-modal">
                {onCancel && <button type="button" className="button button-secondary" onClick={onCancel}>Скасувати</button>}
                <button type="submit" className="button button-primary" disabled={isLoading}>
                    {isLoading ? 'Збереження...' : (initialData ? 'Зберегти' : 'Створити')}
                </button>
            </div>
        </form>
    );
};

export default HabitForm;