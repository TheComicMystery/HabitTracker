import React, { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { getHabits, createHabit, updateHabit, deleteHabit, getCategories, createCategory } from '../services/api';
import HabitListItem from '../components/Habits/HabitListItem';
import HabitForm from '../components/Habits/HabitForm';
import HabitCalendar from '../components/Calendar/HabitCalendar';
import Modal from '../components/UI/Modal';
import { CalendarIcon, FilterIcon, SearchIcon } from '../components/UI/Icons';
import './HabitsPage.css';

const HabitsPage = () => {
    const [habits, setHabits] = useState(null);
    const [isLoadingPage, setIsLoadingPage] = useState(true);
    const [pageError, setPageError] = useState('');
    
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [editingHabit, setEditingHabit] = useState(null);
    const [formIsLoading, setFormIsLoading] = useState(false);
    const [formError, setFormError] = useState('');
    const [activeCategory, setActiveCategory] = useState('all');
    const [searchQuery, setSearchQuery] = useState('');
    const [isCalendarOpen, setIsCalendarOpen] = useState(false);
    const [categoryRecords, setCategoryRecords] = useState([]);

    const fetchHabits = useCallback(async (includeArchived = true) => {
        setIsLoadingPage(true);
        setPageError('');
        try {
            const response = await getHabits(includeArchived);
            if (response && response.data) {
                if (Array.isArray(response.data)) {
                    setHabits(response.data);
                } else {
                    setHabits([]);
                    setPageError('Некоректний формат даних звичок від сервера.');
                }
            } else {
                setHabits([]);
                setPageError('Відповідь від сервера порожня.');
            }
        } catch (err) {
            console.error("Error fetching habits:", err);
            setPageError('Не вдалося завантажити список звичок.');
            setHabits([]);
        } finally {
            setIsLoadingPage(false);
        }
    }, []);

    const handleHabitUpdateSilent = useCallback(async () => {
        try {
            const response = await getHabits(true);
            if (response && Array.isArray(response.data)) {
                setHabits(response.data);
            }
        } catch (err) {
            console.error("Error silent update:", err);
        }
    }, []);

    const fetchCategories = useCallback(async () => {
        try {
            const response = await getCategories();
            setCategoryRecords(Array.isArray(response.data) ? response.data : []);
        } catch (err) {
            console.error("Error fetching categories:", err);
            setCategoryRecords([]);
        }
    }, []);

    useEffect(() => {
        fetchHabits();
        fetchCategories();
    }, [fetchHabits, fetchCategories]);

    const handleOpenCreateModal = () => {
        setEditingHabit(null);
        setFormError('');
        setIsModalOpen(true);
    };

    const handleOpenEditModal = (habitToEdit) => {
        if (habitToEdit && typeof habitToEdit.id !== 'undefined') {
            setEditingHabit(habitToEdit);
            setFormError('');
            setIsModalOpen(true);
        } else {
            setPageError("Не вдалося відкрити форму редагування.");
        }
    };

    const handleCloseModal = () => {
        setIsModalOpen(false);
        setEditingHabit(null);
        setFormError('');
    };

    const handleFormSubmit = async (habitData) => {
        setFormIsLoading(true);
        setFormError('');
        try {
            const nextCategory = (habitData.category || '').trim();
            const knownCategories = categoryRecords.map(item => item.name);
            if (nextCategory && !knownCategories.includes(nextCategory)) {
                await createCategory({ name: nextCategory, color: habitData.color });
                await fetchCategories();
            }

            if (editingHabit && editingHabit.id) {
                await updateHabit(editingHabit.id, habitData);
            } else {
                await createHabit(habitData);
            }
            await fetchHabits();
            handleCloseModal();
        } catch (err) {
            const errorMsg = err.response?.data?.title || err.response?.data?.message || 'Помилка при збереженні звички.';
            setFormError(errorMsg);
        } finally {
            setFormIsLoading(false);
        }
    };

    const handleDeleteHabit = async (habitId) => {
        setFormIsLoading(true);
        setPageError('');
        try {
            await deleteHabit(habitId);
            await fetchHabits();
        } catch (err) {
            setPageError('Помилка при видаленні звички.');
        } finally {
            setFormIsLoading(false);
        }
    };

    const handleArchiveToggle = async (habitId, newArchivedStatus) => {
        setFormIsLoading(true);
        setPageError('');
        try {
            await updateHabit(habitId, { isArchived: newArchivedStatus });
            await fetchHabits();
        } catch (err) {
            setPageError('Помилка при зміні статусу архівації.');
        } finally {
            setFormIsLoading(false);
        }
    };

    if (habits === null && isLoadingPage) {
        return <div className="loading-state">Завантаження звичок...</div>;
    }
    if (habits === null && pageError) {
        return <p className="error-message">{pageError}</p>;
    }

    const habitCategories = habits
        ? habits.map(h => (h.category || '').trim()).filter(Boolean)
        : [];
    const categories = Array.from(new Set([...categoryRecords.map(c => c.name), ...habitCategories])).sort((a, b) => a.localeCompare(b, 'uk'));
    const byCategory = (habit) => activeCategory === 'all' || (habit.category || '').trim() === activeCategory;
    const bySearch = (habit) => {
        const query = searchQuery.trim().toLowerCase();
        if (!query) return true;
        return [habit.name, habit.description, habit.category].some(value => (value || '').toLowerCase().includes(query));
    };
    const activeHabits = habits ? habits.filter(h => h && !h.isArchived && byCategory(h) && bySearch(h)) : [];
    const archivedHabits = habits ? habits.filter(h => h && h.isArchived && byCategory(h) && bySearch(h)) : [];

    return (
        <div className="habits-page-container">
            <div className="habits-content-shell">
                <div className="habits-header">
                    <div>
                        <h1 className="page-title">Мої звички</h1>
                        <p className="page-subtitle">Керуйте звичками, категоріями та виконаннями з одного екрана.</p>
                    </div>
                    <div className="habits-header-actions">
                        <button onClick={() => setIsCalendarOpen(true)} className="button button-secondary calendar-open-btn">
                            <CalendarIcon />
                            <span>Календар</span>
                        </button>
                        <button onClick={handleOpenCreateModal} className="button button-primary add-habit-btn">
                            <span className="button-plus">+</span>
                            <span>Додати звичку</span>
                        </button>
                    </div>
                </div>

                <div className="category-filter-bar">
                    <div className="habit-search-wrap">
                        <SearchIcon />
                        <input
                            className="habit-search-input"
                            value={searchQuery}
                            onChange={e => setSearchQuery(e.target.value)}
                            placeholder="Пошук за назвою або категорією"
                        />
                    </div>
                    <button
                        type="button"
                        className={`category-chip ${activeCategory === 'all' ? 'active' : ''}`}
                        onClick={() => setActiveCategory('all')}
                    >
                        <FilterIcon />
                        <span>Усі</span>
                    </button>
                    {categories.map(category => (
                        <button
                            key={category}
                            type="button"
                            className={`category-chip ${activeCategory === category ? 'active' : ''}`}
                            onClick={() => setActiveCategory(category)}
                        >
                            {category}
                        </button>
                    ))}
                </div>

                {pageError && !isModalOpen && <p className="error-message">{pageError}</p>}

                <h3>Активні звички</h3>
                {isLoadingPage && <p>Оновлення списку...</p>}
                {!isLoadingPage && activeHabits.length === 0 && !pageError && (
                    <p>У вас поки немає активних звичок. <Link to="#" onClick={handleOpenCreateModal}>Створити першу?</Link></p>
                )}
                <div className="habits-list">
                    {activeHabits.map(habit => (
                        <HabitListItem
                            key={habit.id}
                            habit={habit}
                            onEdit={handleOpenEditModal}
                            onDelete={handleDeleteHabit}
                            onArchiveToggle={handleArchiveToggle}
                            onHabitUpdated={handleHabitUpdateSilent}
                        />
                    ))}
                </div>

                {archivedHabits.length > 0 && (
                    <>
                        <h3 style={{marginTop: '30px'}}>Архівні звички</h3>
                        <div className="habits-list archived">
                            {archivedHabits.map(habit => (
                                <HabitListItem
                                    key={habit.id}
                                    habit={habit}
                                    onEdit={handleOpenEditModal}
                                    onDelete={handleDeleteHabit}
                                    onArchiveToggle={handleArchiveToggle}
                                    onHabitUpdated={handleHabitUpdateSilent}
                                />
                            ))}
                        </div>
                    </>
                )}
            </div>

            {isModalOpen && (
                <Modal onClose={handleCloseModal} title={editingHabit ? "Редагувати звичку" : "Нова звичка"}>
                    <HabitForm
                        onSubmit={handleFormSubmit}
                        initialData={editingHabit}
                        onCancel={handleCloseModal}
                        isLoading={formIsLoading}
                        availableCategories={categories}
                    />
                    {formError && <p className="error-message" style={{marginTop: '15px'}}>{formError}</p>}
                </Modal>
            )}

            {isCalendarOpen && (
                <Modal onClose={() => setIsCalendarOpen(false)} title={<><CalendarIcon /> <span>Календар звичок</span></>}>
                    <div className="habits-calendar-modal">
                        <HabitCalendar onHabitUpdated={handleHabitUpdateSilent} />
                    </div>
                </Modal>
            )}
        </div>
    );
};

export default HabitsPage;
