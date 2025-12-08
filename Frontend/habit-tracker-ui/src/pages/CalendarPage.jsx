import React, { useState, useEffect, useCallback } from 'react';
import Calendar from 'react-calendar';
import 'react-calendar/dist/Calendar.css';
import { getHabits, getHabitEntries, trackHabit } from '../services/api';
import { UndoIcon, CheckIcon } from '../components/UI/Icons'; 
import './CalendarPage.css';

const formatDateForAPI = (date) => {
    const d = new Date(date);
    const year = d.getFullYear();
    const month = (`0${d.getMonth() + 1}`).slice(-2);
    const day = (`0${d.getDate()}`).slice(-2);
    return `${year}-${month}-${day}`;
};

const CalendarPage = () => {
    const [currentMonthView, setCurrentMonthView] = useState(new Date());
    const [selectedDate, setSelectedDate] = useState(new Date());
    const [activeHabits, setActiveHabits] = useState([]);
    const [habitEntriesByDate, setHabitEntriesByDate] = useState({});
    const [selectedDayHabitDetails, setSelectedDayHabitDetails] = useState([]);
    const [isLoadingCalendarData, setIsLoadingCalendarData] = useState(false);
    const [isLoadingDayDetails, setIsLoadingDayDetails] = useState(false);
    const [trackingStates, setTrackingStates] = useState({});

    const fetchAllActiveHabits = useCallback(async () => {
        try {
            const response = await getHabits(false);
            setActiveHabits(Array.isArray(response.data) ? response.data : []);
        } catch (err) {
            console.error("Error fetching habits:", err);
            setActiveHabits([]);
        }
    }, []);

    useEffect(() => {
        fetchAllActiveHabits();
    }, [fetchAllActiveHabits]);

    const loadEntriesForVisibleMonth = useCallback(async (dateForMonth) => {
        if (activeHabits.length === 0) return;
        setIsLoadingCalendarData(true);
        
        const year = dateForMonth.getFullYear();
        const month = dateForMonth.getMonth();
        const startDateOfMonth = formatDateForAPI(new Date(year, month, 1));
        const endDateOfMonth = formatDateForAPI(new Date(year, month + 1, 0));
        const newEntriesForThisMonth = {};
    
        try {
            const entryPromises = activeHabits.map(habit =>
                getHabitEntries(habit.id, startDateOfMonth, endDateOfMonth)
                    .then(response => ({ habitId: habit.id, entries: response.data }))
                    .catch(() => ({ habitId: habit.id, entries: [] }))
            );
            const results = await Promise.all(entryPromises);
            results.forEach(result => {
                result.entries.forEach(entry => {
                    const entryDateStr = formatDateForAPI(new Date(entry.date));
                    if (!newEntriesForThisMonth[entryDateStr]) newEntriesForThisMonth[entryDateStr] = [];
                    newEntriesForThisMonth[entryDateStr].push({ ...entry, habitId: result.habitId });
                });
            });
            setHabitEntriesByDate(prev => ({...prev, ...newEntriesForThisMonth}));
        } catch (err) {
            console.error(err);
        } finally {
            setIsLoadingCalendarData(false);
        }
    }, [activeHabits]);

    useEffect(() => {
        if (activeHabits.length > 0) loadEntriesForVisibleMonth(currentMonthView);
    }, [activeHabits, currentMonthView, loadEntriesForVisibleMonth]);

    const updateSelectedDayDetails = useCallback((date) => {
        setIsLoadingDayDetails(true);
        const dateStr = formatDateForAPI(date);
        const dayOfWeek = date.getDay();
        const entriesForSelectedDay = habitEntriesByDate[dateStr] || [];
        
        const details = activeHabits
            .filter(habit => {
                const habitStartDate = new Date(habit.startDate); habitStartDate.setHours(0,0,0,0);
                const selectedDateOnly = new Date(date); selectedDateOnly.setHours(0,0,0,0);
                return habit.activeDays.includes(dayOfWeek) && habitStartDate <= selectedDateOnly;
            })
            .map(habit => {
                const entry = entriesForSelectedDay.find(e => e.habitId === habit.id);
                return {
                    id: habit.id,
                    name: habit.name,
                    color: habit.color,
                    targetCount: habit.targetCount,
                    completedCount: entry?.completedCount || 0,
                    isFullyCompleted: entry?.isFullyCompleted || false,
                };
            });
        setSelectedDayHabitDetails(details);
        setIsLoadingDayDetails(false);
    }, [activeHabits, habitEntriesByDate]);

    useEffect(() => {
        updateSelectedDayDetails(selectedDate);
    }, [selectedDate, habitEntriesByDate, updateSelectedDayDetails]);

    const handleTrackCalendarHabit = async (habitId, currentCompleted, targetCount, dateToTrack, increment = true) => {
        setTrackingStates(prev => ({ ...prev, [habitId]: true }));
        let newCompletedCount = increment ? Math.min(currentCompleted + 1, targetCount) : Math.max(currentCompleted - 1, 0);
        
        try {
            await trackHabit(habitId, { date: dateToTrack, completedCount: newCompletedCount });
            const dateStr = formatDateForAPI(dateToTrack);
            
            setHabitEntriesByDate(prev => {
                const dayEntries = prev[dateStr] || [];
                const existingEntryIndex = dayEntries.findIndex(e => e.habitId === habitId);
                const newEntry = { 
                    habitId, date: dateToTrack, completedCount: newCompletedCount, 
                    isFullyCompleted: newCompletedCount >= targetCount 
                };
                let updatedDayEntries = existingEntryIndex >= 0 ? [...dayEntries] : [...dayEntries, newEntry];
                if (existingEntryIndex >= 0) updatedDayEntries[existingEntryIndex] = newEntry;
                return { ...prev, [dateStr]: updatedDayEntries };
            });
        } catch (err) {
            console.error(err);
        } finally {
            setTrackingStates(prev => ({ ...prev, [habitId]: false }));
        }
    };

    const tileClassName = ({ date, view }) => {
        if (view === 'month') {
            const dateStr = formatDateForAPI(date);
            const entries = habitEntriesByDate[dateStr] || [];
            const dayOfWeek = date.getDay();
            const dayHabits = activeHabits.filter(h => {
                const start = new Date(h.startDate); start.setHours(0,0,0,0);
                const curr = new Date(date); curr.setHours(0,0,0,0);
                return h.activeDays.includes(dayOfWeek) && start <= curr;
            });

            if (dayHabits.length === 0) return null;
            const allDone = dayHabits.every(h => entries.find(e => e.habitId === h.id)?.isFullyCompleted);
            if (allDone) return 'day-all-completed';
            const someDone = dayHabits.some(h => entries.find(e => e.habitId === h.id)?.completedCount > 0);
            if (someDone) return 'day-some-completed';
            if (dayHabits.length > 0 && entries.length === 0) return 'day-pending';
        }
        return null;
    };

    const tileContent = ({ date, view }) => {
        if (view === 'month') {
            const dateStr = formatDateForAPI(date);
            const entries = habitEntriesByDate[dateStr] || [];
            const dayOfWeek = date.getDay();
            
            const dayHabits = activeHabits.filter(h => {
                const start = new Date(h.startDate); start.setHours(0,0,0,0);
                const curr = new Date(date); curr.setHours(0,0,0,0);
                return h.activeDays.includes(dayOfWeek) && start <= curr;
            });

            if (dayHabits.length === 0) return null;

            const completed = dayHabits.filter(h => entries.find(e => e.habitId === h.id)?.isFullyCompleted).length;
            const total = dayHabits.length;

            return (
                <div className={`tile-progress-text ${completed === total ? 'text-done' : ''}`}>
                    {completed}/{total}
                </div>
            );
        }
        return null;
    };

    return (
        <div className="calendar-page-container">
            <h1 className="page-title">Календар Звичок</h1>
            <div className="calendar-view-wrapper card">
                <Calendar
                    onChange={setSelectedDate}
                    onActiveStartDateChange={({ activeStartDate }) => setCurrentMonthView(activeStartDate)}
                    value={selectedDate}
                    tileClassName={tileClassName}
                    tileContent={tileContent}
                    locale="uk-UK"
                />
                {isLoadingCalendarData && <div className="calendar-loading-overlay">...</div>}
            </div>

            <div className="selected-date-details-wrapper card">
                <h3>{selectedDate.toLocaleDateString('uk-UA', { day: 'numeric', month: 'long' })}</h3>
                {selectedDayHabitDetails.length === 0 ? (
                    <p className="no-data-message">Немає звичок на цей день</p>
                ) : (
                    <ul className="calendar-day-habits-list">
                        {selectedDayHabitDetails.map(habit => {
                            const isTracking = trackingStates[habit.id];
                            return (
                                <li key={habit.id} className={`calendar-day-habit-item ${habit.isFullyCompleted ? 'completed' : ''}`}>
                                    <span className="habit-name" style={{borderLeftColor: habit.color}}>{habit.name}</span>
                                    <span className="habit-status-text">{habit.completedCount} / {habit.targetCount}</span>
                                    
                                    <div className="calendar-habit-actions">
                                        {habit.targetCount === 1 ? (
                                            <button 
                                                onClick={() => handleTrackCalendarHabit(habit.id, habit.completedCount, habit.targetCount, selectedDate, !habit.isFullyCompleted)}
                                                className={`button-icon ${habit.isFullyCompleted ? 'active' : ''}`}
                                                disabled={isTracking}
                                                title={habit.isFullyCompleted ? "Виконано" : "Виконати"}
                                            >
                                                <CheckIcon />
                                            </button>
                                        ) : (
                                            <>
                                                <button 
                                                    onClick={() => handleTrackCalendarHabit(habit.id, habit.completedCount, habit.targetCount, selectedDate, false)}
                                                    className="button-icon secondary"
                                                    disabled={habit.completedCount <= 0 || isTracking}
                                                    title="Відмінити (зменшити)"
                                                >
                                                    <UndoIcon />
                                                </button>
                                                <button 
                                                    onClick={() => handleTrackCalendarHabit(habit.id, habit.completedCount, habit.targetCount, selectedDate, true)}
                                                    className="button-icon primary"
                                                    disabled={habit.completedCount >= habit.targetCount || isTracking}
                                                    title="Додати раз"
                                                >
                                                    <CheckIcon />
                                                </button>
                                            </>
                                        )}
                                    </div>
                                </li>
                            );
                        })}
                    </ul>
                )}
            </div>
        </div>
    );
};

export default CalendarPage;