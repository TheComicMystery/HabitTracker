import React, { useState } from 'react';
import { createPortal } from 'react-dom';
import { EditIcon, TrashIcon, ArchiveIcon, UnarchiveIcon, MagicIcon, CloseIcon } from '../UI/Icons';
import { trackHabit, logHabitConfidence } from '../../services/api';
import './HabitListItem.css';

const daysOfWeekShort = ["Нд", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб"];

const HabitListItem = ({ habit, onEdit, onDelete, onArchiveToggle, onHabitUpdated }) => {
    const [showFailReasonModal, setShowFailReasonModal] = useState(false);
    const [confidenceSubmitted, setConfidenceSubmitted] = useState(false);

    const isUncertain = habit.successProbability >= 35 && habit.successProbability <= 65;

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

    const handleSkipClick = () => {
        setShowFailReasonModal(true);
    };

    const handleReasonSubmit = async (reason) => {
        try {
            await trackHabit(habit.id, {
                date: new Date(),
                completedCount: 0,
                failureReason: reason
            });
            setShowFailReasonModal(false);
            if (onHabitUpdated) {
                await onHabitUpdated();
            }
        } catch (error) {
            console.error(error);
        }
    };

    const handleConfidenceSubmit = async (score) => {
        try {
            await logHabitConfidence(habit.id, {
                date: new Date(),
                score: score
            });
            setConfidenceSubmitted(true);
        } catch (error) {
            console.error(error);
        }
    };

    const FailureModal = () => (
        <div className="modal-backdrop" onClick={(e) => { if(e.target === e.currentTarget) setShowFailReasonModal(false); }}>
            <div className="modal-content failure-modal">
                <h3 style={{marginTop: 0}}>Чому не вийшло?</h3>
                <p style={{color: 'var(--text-secondary)', marginBottom: '20px'}}>
                    Чесна відповідь допоможе алгоритму краще підлаштуватися під ваш ритм.
                </p>
                <div style={{display: 'flex', flexDirection: 'column', gap: '10px'}}>
                    {['Втома / Стрес', 'Брак часу', 'Лінь / Прокрастинація', 'Забув(ла)', 'Хвороба', 'Свято / Гості'].map(reason => (
                        <button 
                            key={reason} 
                            onClick={() => handleReasonSubmit(reason)}
                            className="button button-secondary"
                            style={{textAlign: 'left', justifyContent: 'flex-start'}}
                        >
                            {reason}
                        </button>
                    ))}
                    <button 
                        onClick={() => setShowFailReasonModal(false)} 
                        className="button" 
                        style={{marginTop: '10px'}}
                    >
                        Скасувати
                    </button>
                </div>
            </div>
        </div>
    );

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
                        <div className="ml-tooltip-container" style={{ position: 'relative', display: 'inline-block' }}>
                            <span className="meta-tag ml-tag" style={{
                                border: `1px solid ${getPredictionColor(habit.successProbability)}`,
                                color: getPredictionColor(habit.successProbability),
                                background: `linear-gradient(90deg, ${getPredictionColor(habit.successProbability)}10 0%, transparent 100%)`,
                                cursor: 'help'
                            }}>
                                <MagicIcon />
                                <span>Прогноз: <strong>{habit.successProbability}%</strong></span>
                            </span>
                            
                            {habit.shapExplanation && (
                                <div className="shap-tooltip" style={{
                                    position: 'absolute', bottom: '110%', left: '0',
                                    background: 'var(--text-primary)', color: 'var(--bg-primary)',
                                    padding: '10px 14px', borderRadius: '8px', fontSize: '0.85rem',
                                    width: 'max-content', maxWidth: '300px', whiteSpace: 'normal',
                                    boxShadow: 'var(--shadow-md)', zIndex: 10,
                                    opacity: 0, visibility: 'hidden', transition: 'all 0.2s',
                                    lineHeight: '1.4'
                                }}>
                                    {habit.shapExplanation}
                                </div>
                            )}
                        </div>
                    )}
                </div>

                {!habit.isArchived && isUncertain && !confidenceSubmitted && (
                    <div className="uncertainty-prompt">
                        <p>ШІ сумнівається (шанс {habit.successProbability}%). Наскільки ви впевнені, що виконаєте це сьогодні?</p>
                        <div className="confidence-scales">
                            {[1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map(n => (
                                <button
                                    key={n}
                                    onClick={() => handleConfidenceSubmit(n)}
                                    className="conf-btn"
                                >
                                    {n}
                                </button>
                            ))}
                        </div>
                    </div>
                )}
            </div>

            <div className="habit-actions">
                {!habit.isArchived && (
                    <button onClick={handleSkipClick} className="action-btn delete" title="Зафіксувати пропуск">
                        <CloseIcon />
                    </button>
                )}
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

            {showFailReasonModal && createPortal(<FailureModal />, document.body)}
        </div>
    );
};

export default HabitListItem;