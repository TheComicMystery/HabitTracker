import React, { useMemo, useState } from 'react';
import { AreaChart, Area, XAxis, Tooltip, ResponsiveContainer, CartesianGrid } from 'recharts';
import { ChevronLeftIcon, ChevronRightIcon } from '../UI/Icons';
import './AnalyticsWidgets.css';

const UKRAINIAN_MONTHS = ['Січень', 'Лютий', 'Березень', 'Квітень', 'Травень', 'Червень', 'Липень', 'Серпень', 'Вересень', 'Жовтень', 'Листопад', 'Грудень'];
const WEEKDAYS = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Нд'];

const AnalyticsWidgets = ({ habits, entries, onDateClick }) => {
    const [viewDate, setViewDate] = useState(new Date());

    const calendarGrid = useMemo(() => {
        const year = viewDate.getFullYear();
        const month = viewDate.getMonth();
        
        const firstDayOfMonth = new Date(year, month, 1);
        
        let startDayOfWeek = firstDayOfMonth.getDay(); 
        if (startDayOfWeek === 0) startDayOfWeek = 7; 
        
        const startDate = new Date(firstDayOfMonth);
        startDate.setDate(firstDayOfMonth.getDate() - (startDayOfWeek - 1));

        const days = [];
        for (let i = 0; i < 42; i++) {
            const current = new Date(startDate);
            current.setDate(startDate.getDate() + i);
            days.push(current);
        }
        return days;
    }, [viewDate]);

    const activityMap = useMemo(() => {
        const map = {};
        if (entries && Array.isArray(entries)) {
            entries.forEach(entry => {
                if (!entry.date) return;
                const dateStr = new Date(entry.date).toISOString().split('T')[0];
                if (!map[dateStr]) map[dateStr] = 0;
                map[dateStr] += 1;
            });
        }
        return map;
    }, [entries]);

    const handlePrevMonth = () => {
        const newDate = new Date(viewDate);
        newDate.setMonth(newDate.getMonth() - 1);
        setViewDate(newDate);
    };

    const handleNextMonth = () => {
        const newDate = new Date(viewDate);
        newDate.setMonth(newDate.getMonth() + 1);
        setViewDate(newDate);
    };

    const isCurrentMonth = (date) => {
        return date.getMonth() === viewDate.getMonth();
    };

    const getColorClass = (count) => {
        if (!count) return 'level-0';
        if (count >= 10) return 'level-4';
        if (count >= 7) return 'level-3';
        if (count >= 4) return 'level-2';
        return 'level-1';
    };

    const chartData = useMemo(() => {
        const data = [];
        const now = new Date();
        for (let i = 13; i >= 0; i--) {
            const d = new Date(now);
            d.setDate(d.getDate() - i);
            const dateStr = d.toISOString().split('T')[0];
            const dayEntries = Array.isArray(entries) ? entries.filter(e => e.date && e.date.startsWith(dateStr)) : [];
            const totalActivity = dayEntries.reduce((acc, curr) => acc + (curr.isFullyCompleted ? 10 : 5), 0);
            data.push({
                name: d.toLocaleDateString('uk-UA', { day: 'numeric', month: 'short' }),
                score: totalActivity
            });
        }
        return data;
    }, [entries]);

    const CustomChartTooltip = ({ active, payload, label }) => {
        if (active && payload && payload.length) {
            return (
                <div className="custom-tooltip">
                    <p className="tooltip-label">{label}</p>
                    <p className="tooltip-score">Бали: <strong>{payload[0].value}</strong></p>
                </div>
            );
        }
        return null;
    };

    const isFuture = () => {
        const today = new Date();
        return viewDate.getMonth() === today.getMonth() && viewDate.getFullYear() === today.getFullYear();
    };

    return (
        <div className="analytics-grid">
            <div className="bento-item analytics-card heatmap-card">
                <div className="card-header-nav">
                    <h3>{UKRAINIAN_MONTHS[viewDate.getMonth()]} {viewDate.getFullYear()}</h3>
                    <div className="nav-buttons">
                        <button onClick={handlePrevMonth} className="nav-btn"><ChevronLeftIcon /></button>
                        <button onClick={handleNextMonth} className="nav-btn" disabled={isFuture()}><ChevronRightIcon /></button>
                    </div>
                </div>
                
                <div className="custom-calendar-grid">
                    {WEEKDAYS.map(day => (
                        <div key={day} className="weekday-label">{day}</div>
                    ))}
                    
                    {calendarGrid.map((date, idx) => {
                        const dateStr = date.toISOString().split('T')[0];
                        const count = activityMap[dateStr] || 0;
                        const isCurrent = isCurrentMonth(date);
                        
                        return (
                            <div 
                                key={idx}
                                className={`calendar-day ${getColorClass(count)} ${!isCurrent ? 'other-month' : ''}`}
                                title={`${date.toLocaleDateString('uk-UA')}: ${count} дій`}
                                onClick={() => onDateClick && onDateClick(date)}
                            ></div>
                        );
                    })}
                </div>
                
                <div className="heatmap-legend">
                    <span>Менше</span>
                    <div className="legend-items">
                        <div className="legend-box level-0"></div>
                        <div className="legend-box level-1"></div>
                        <div className="legend-box level-2"></div>
                        <div className="legend-box level-3"></div>
                        <div className="legend-box level-4"></div>
                    </div>
                    <span>Більше</span>
                </div>
            </div>

            <div className="bento-item analytics-card chart-card">
                <div className="card-header-nav">
                    <h3>Продуктивність</h3>
                    <span className="subtitle-small">Останні 14 днів</span>
                </div>
                <div className="chart-wrapper">
                    <ResponsiveContainer width="100%" height="100%">
                        <AreaChart data={chartData}>
                            <defs>
                                <linearGradient id="colorScore" x1="0" y1="0" x2="0" y2="1">
                                    <stop offset="5%" stopColor="#6366f1" stopOpacity={0.8}/>
                                    <stop offset="95%" stopColor="#6366f1" stopOpacity={0}/>
                                </linearGradient>
                            </defs>
                            <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--border-color)" opacity={0.5} />
                            <XAxis dataKey="name" tick={{fontSize: 10, fill: 'var(--text-secondary)'}} axisLine={false} tickLine={false} interval={2} />
                            <Tooltip content={<CustomChartTooltip />} cursor={{ stroke: 'var(--text-muted)', strokeWidth: 1, strokeDasharray: '4 4' }}/>
                            <Area type="monotone" dataKey="score" stroke="#6366f1" strokeWidth={2} fillOpacity={1} fill="url(#colorScore)" />
                        </AreaChart>
                    </ResponsiveContainer>
                </div>
            </div>
        </div>
    );
};

export default AnalyticsWidgets;