import React from 'react';
import { EditIcon, TrashIcon, ArchiveIcon, UnarchiveIcon, MagicIcon } from '../UI/Icons';
import './HabitListItem.css';

const daysOfWeekShort = ["Нд", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб"];

const HabitListItem = ({ habit, onEdit, onDelete, onArchiveToggle }) => {
    const handleDelete = () => {
        if (window.confirm(`Видалити "${habit.name}"?`)) {
            onDelete(habit.id);
        }
    };

    const handleArchive = () => {
        onArchiveToggle(habit.id, !habit.isArchived);
    }

    const getPredictionColor = (prob) => {
        if (prob >= 80) return '#10b981';
        if (prob >= 50) return '#f59e0b';
        return '#ef4444';
    };

    return (
        <div className={`habit-list-item ${habit.isArchived ? 'archived' : ''}`}>
            <div className="habit-indicator" style={{ backgroundColor: habit.color || 'var(--color-primary)' }}></div>
            
            <div className="habit-content">
                <div className="habit-header">
                    <div style={{display: 'flex', alignItems: 'center', gap: '8px'}}>
                        <span style={{fontSize: '1.2rem'}}>{habit.icon || '📌'}</span>
                        <h4 className="habit-name">{habit.name}</h4>
                    </div>
                    {habit.isArchived && <span className="badge-archived">Архів</span>}
                </div>
                
                {habit.description && <p className="habit-desc">{habit.description}</p>}
                
                <div className="habit-meta">
                    <span className="meta-tag">🎯 {habit.targetCount}/день</span>
                    <span className="meta-tag">📅 {habit.activeDays.length === 7 ? 'Щодня' : habit.activeDays.map(d => daysOfWeekShort[d]).join(' ')}</span>
                    
                    {!habit.isArchived && habit.successProbability !== undefined && (
                        <span className="meta-tag ml-tag" style={{
                            border: `1px solid ${getPredictionColor(habit.successProbability)}`,
                            color: getPredictionColor(habit.successProbability),
                            background: `linear-gradient(90deg, ${getPredictionColor(habit.successProbability)}10 0%, transparent 100%)`
                        }}>
                            <MagicIcon /> 
                            <span>Ймовірність успіху: <strong>{habit.successProbability}%</strong></span>
                        </span>
                    )}
                </div>
            </div>

            <div className="habit-actions">
                <button onClick={() => onEdit(habit)} className="action-btn edit" title="Редагувати">
                    <EditIcon />
                </button>
                <button onClick={handleArchive} className="action-btn archive" title={habit.isArchived ? "Розархівувати" : "Архівувати"}>
                    {habit.isArchived ? <UnarchiveIcon /> : <ArchiveIcon />}
                </button>
                <button onClick={handleDelete} className="action-btn delete" title="Видалити">
                    <TrashIcon />
                </button>
            </div>
        </div>
    );
};

export default HabitListItem;