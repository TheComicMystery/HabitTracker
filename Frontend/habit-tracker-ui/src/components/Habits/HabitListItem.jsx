import React, { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { EditIcon, TrashIcon, ArchiveIcon, UnarchiveIcon, MagicIcon, CloseIcon } from '../UI/Icons';
import { trackHabit, logHabitConfidence } from '../../services/api';
import './HabitListItem.css';

const daysOfWeekShort = ['Нд', 'Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб'];

const HabitListItem = ({ habit, onEdit, onDelete, onArchiveToggle, onHabitUpdated }) => {
    const [showFailReasonModal, setShowFailReasonModal] = useState(false);
    const [confidenceSubmitted, setConfidenceSubmitted] = useState(false);
    const [showConfidencePrompt, setShowConfidencePrompt] = useState(false);
    const [pendingConfidenceScore, setPendingConfidenceScore] = useState(null);

    const isUncertain = Boolean(habit.shouldAskConfidence);

    useEffect(() => {
        if (!isUncertain || confidenceSubmitted || !habit.id) {
            setShowConfidencePrompt(false);
            return;
        }

        const key = `habit-confidence-prompt:${habit.id}`;
        const now = Date.now();
        const day = 24 * 60 * 60 * 1000;
        const cooldownMs = 3 * day;
        const windowMs = 14 * day;
        let history = { impressions: [] };

        try {
            history = JSON.parse(localStorage.getItem(key) || '{"impressions":[]}');
        } catch {
            history = { impressions: [] };
        }

        const impressions = Array.isArray(history.impressions)
            ? history.impressions.filter(timestamp => now - timestamp < windowMs)
            : [];
        const recentlyShown = impressions.some(timestamp => now - timestamp < cooldownMs);
        const tooManyShows = impressions.length >= 2;

        if (recentlyShown || tooManyShows) {
            localStorage.setItem(key, JSON.stringify({ impressions }));
            setShowConfidencePrompt(false);
            return;
        }

        impressions.push(now);
        localStorage.setItem(key, JSON.stringify({ impressions }));
        setShowConfidencePrompt(true);
    }, [habit.id, isUncertain, confidenceSubmitted]);

    const handleDelete = () => {
        if (window.confirm(`Видалити "${habit.name}"?`)) {
            onDelete(habit.id);
        }
    };

    const handleArchive = () => {
        onArchiveToggle(habit.id, !habit.isArchived);
    };

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

    const confidenceReasons = [
        { value: 'time', label: 'Брак часу' },
        { value: 'energy', label: 'Втома' },
        { value: 'mood', label: 'Настрій' },
        { value: 'motivation', label: 'Не тягне' },
        { value: 'events', label: 'Плани / гості' },
        { value: 'forgetting', label: 'Можу забути' }
    ];

    const handleConfidenceClick = (score) => {
        if (score <= 6) {
            setPendingConfidenceScore(score);
            return;
        }
        handleConfidenceSubmit(score);
    };

    const handleConfidenceSubmit = async (score, reason = null) => {
        try {
            await logHabitConfidence(habit.id, {
                date: new Date(),
                score: score,
                reason: reason
            });
            setConfidenceSubmitted(true);
            setShowConfidencePrompt(false);
            setPendingConfidenceScore(null);
        } catch (error) {
            console.error(error);
        }
    };

    const FailureModal = () => (
        <div className="modal-backdrop" onClick={(e) => { if (e.target === e.currentTarget) setShowFailReasonModal(false); }}>
            <div className="modal-content failure-modal">
                <h3 style={{ marginTop: 0 }}>Чому не вийшло?</h3>
                <p style={{ color: 'var(--text-secondary)', marginBottom: '20px' }}>
                    Чесна відповідь допоможе алгоритму краще підлаштуватися під ваш ритм.
                </p>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    {['Втома / стрес', 'Брак часу', 'Лінь / прокрастинація', 'Забув(ла)', 'Хвороба', 'Свято / гості'].map(reason => (
                        <button
                            key={reason}
                            onClick={() => handleReasonSubmit(reason)}
                            className="button button-secondary"
                            style={{ textAlign: 'left', justifyContent: 'flex-start' }}
                        >
                            {reason}
                        </button>
                    ))}
                    <button
                        onClick={() => setShowFailReasonModal(false)}
                        className="button"
                        style={{ marginTop: '10px' }}
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
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span style={{ fontSize: '1.2rem' }}>{habit.icon || '📌'}</span>
                        <h4 className="habit-name">{habit.name}</h4>
                    </div>
                    {habit.isArchived && <span className="badge-archived">Архів</span>}
                </div>

                {habit.description && <p className="habit-desc">{habit.description}</p>}

                <div className="habit-meta">
                    {habit.category && <span className="meta-tag category-tag">{habit.category}</span>}
                    <span className="meta-tag">🎯 {habit.targetCount}/день</span>
                    <span className="meta-tag">🗓️ {habit.activeDays.length === 7 ? 'Щодня' : habit.activeDays.map(d => daysOfWeekShort[d]).join(' ')}</span>

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

                {!habit.isArchived && showConfidencePrompt && (
                    <div className="uncertainty-prompt">
                        <p>Прогноз зараз невпевнений ({habit.successProbability}%). Наскільки реально виконати це сьогодні?</p>
                        <div className="confidence-scales">
                            {[1, 2, 3, 4, 5, 6, 7, 8, 9, 10].map(n => (
                                <button
                                    key={n}
                                    onClick={() => handleConfidenceClick(n)}
                                    className={`conf-btn ${pendingConfidenceScore === n ? 'selected' : ''}`}
                                >
                                    {n}
                                </button>
                            ))}
                        </div>
                        {pendingConfidenceScore && (
                            <div className="confidence-reasons">
                                <span>Що найбільше знижує впевненість?</span>
                                <div>
                                    {confidenceReasons.map(reason => (
                                        <button key={reason.value} type="button" onClick={() => handleConfidenceSubmit(pendingConfidenceScore, reason.value)}>
                                            {reason.label}
                                        </button>
                                    ))}
                                    <button type="button" onClick={() => handleConfidenceSubmit(pendingConfidenceScore, 'unspecified')}>
                                        Не уточнювати
                                    </button>
                                </div>
                            </div>
                        )}
                    </div>
                )}
            </div>

            <div className="habit-actions">
                {!habit.isArchived && (
                    <button onClick={handleSkipClick} className="action-btn action-text skip" title="Зафіксувати пропуск">
                        <CloseIcon />
                        <span>Пропуск</span>
                    </button>
                )}
                <button onClick={() => onEdit(habit)} className="action-btn edit" title="Редагувати">
                    <EditIcon />
                </button>
                <button onClick={handleArchive} className="action-btn archive" title={habit.isArchived ? 'Розархівувати' : 'Архівувати'}>
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
