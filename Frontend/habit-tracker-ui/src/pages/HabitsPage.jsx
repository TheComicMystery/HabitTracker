import React, { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { getHabits, createHabit, updateHabit, deleteHabit } from '../services/api';
import HabitListItem from '../components/Habits/HabitListItem';
import HabitForm from '../components/Habits/HabitForm';
import Modal from '../components/UI/Modal';
import './HabitsPage.css';

const HabitsPage = () => {
    const [habits, setHabits] = useState(null);
    const [isLoadingPage, setIsLoadingPage] = useState(true);
    const [pageError, setPageError] = useState('');
    
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [editingHabit, setEditingHabit] = useState(null);
    const [formIsLoading, setFormIsLoading] = useState(false);
    const [formError, setFormError] = useState('');

    console.log("%cHabitsPage: Render/Re-render", "color: blue; font-weight: bold;", { isLoadingPage, isModalOpen, habitsCount: habits ? habits.length : 'null' });

    const fetchHabits = useCallback(async (includeArchived = true) => {
        console.log("%cHabitsPage: fetchHabits START", "color: green;", { includeArchived });
        setIsLoadingPage(true);
        setPageError('');
        try {
            const response = await getHabits(includeArchived);
            console.log("%cHabitsPage: API response for getHabits:", "color: green;", response);

            if (response && response.data) {
                if (Array.isArray(response.data)) {
                    console.log("%cHabitsPage: API response.data IS an array. Length:", "color: green;", response.data.length);
                    setHabits(response.data);
                } else {
                    console.error("%cHabitsPage: API response.data IS NOT an array!", "color: red; font-weight: bold;", response.data);
                    setHabits([]);
                    setPageError('Некоректний формат даних звичок від сервера (очікувався масив).');
                }
            } else {
                console.error("%cHabitsPage: API response or response.data is undefined/null!", "color: red; font-weight: bold;", response);
                setHabits([]);
                setPageError('Відповідь від сервера порожня або некоректна.');
            }
        } catch (err) {
            console.error("%cHabitsPage: Error fetching habits:", "color: red; font-weight: bold;", err.response || err.message || err);
            setPageError('Не вдалося завантажити список звичок. Дивіться консоль для деталей.');
            setHabits([]);
        } finally {
            setIsLoadingPage(false);
            console.log("%cHabitsPage: fetchHabits END", "color: green;");
        }
    }, []);

    useEffect(() => {
        console.log("%cHabitsPage: useEffect for initial fetchHabits triggered.", "color: purple;");
        fetchHabits();
    }, [fetchHabits]);

    const handleOpenCreateModal = () => {
        console.log("%cHabitsPage: handleOpenCreateModal", "color: orange;");
        setEditingHabit(null);
        setFormError('');
        setIsModalOpen(true);
    };

    const handleOpenEditModal = (habitToEdit) => {
        console.log("%cHabitsPage: handleOpenEditModal", "color: orange;", { habitToEdit });
        if (habitToEdit && typeof habitToEdit.id !== 'undefined') {
            setEditingHabit(habitToEdit);
            setFormError('');
            setIsModalOpen(true);
        } else {
            console.error("%cHabitsPage: Attempted to edit invalid habit data:", "color: red;", { habitToEdit });
            setPageError("Не вдалося відкрити форму редагування: некоректні дані звички.");
        }
    };

    const handleCloseModal = () => {
        console.log("%cHabitsPage: handleCloseModal", "color: orange;");
        setIsModalOpen(false);
        setEditingHabit(null);
        setFormError('');
    };

    const handleFormSubmit = async (habitData) => {
        console.log("%cHabitsPage: handleFormSubmit START", "color: brown;", { editingHabit, habitData });
        setFormIsLoading(true);
        setFormError('');
        try {
            if (editingHabit && editingHabit.id) {
                await updateHabit(editingHabit.id, habitData);
            } else {
                await createHabit(habitData);
            }
            await fetchHabits();
            handleCloseModal();
        } catch (err) {
            console.error("%cHabitsPage: Error submitting habit form:", "color: red;", err.response || err.message || err);
            const errorMsg = err.response?.data?.title || err.response?.data?.message || 'Помилка при збереженні звички.';
            setFormError(errorMsg);
        } finally {
            setFormIsLoading(false);
            console.log("%cHabitsPage: handleFormSubmit END", "color: brown;");
        }
    };

    const handleDeleteHabit = async (habitId) => {
        console.log("%cHabitsPage: handleDeleteHabit START", "color: darkred;", { habitId });
        setFormIsLoading(true);
        setPageError('');
        try {
            await deleteHabit(habitId);
            await fetchHabits();
        } catch (err) {
            console.error("%cHabitsPage: Error deleting habit:", "color: red;", err.response || err.message || err);
            setPageError('Помилка при видаленні звички.');
        } finally {
            setFormIsLoading(false);
        }
    };

    const handleArchiveToggle = async (habitId, newArchivedStatus) => {
        console.log("%cHabitsPage: handleArchiveToggle START", "color: goldenrod;", { habitId, newArchivedStatus });
        setFormIsLoading(true);
        setPageError('');
        try {
            await updateHabit(habitId, { isArchived: newArchivedStatus });
            await fetchHabits();
        } catch (err) {
            console.error("%cHabitsPage: Error archiving habit:", "color: red;", err.response || err.message || err);
            setPageError('Помилка при зміні статусу архівації.');
        } finally {
            setFormIsLoading(false);
        }
    };

    console.log("%cHabitsPage: State before render", "color: blue;", { habits, isLoadingPage, pageError });

    if (habits === null && isLoadingPage) {
        return <div className="loading-state">Завантаження звичок...</div>;
    }
    if (habits === null && pageError) {
        return <p className="error-message">{pageError}</p>;
    }
    if (!Array.isArray(habits)) {
        console.error("%cHabitsPage: Critical - habits is not an array before render loop!", "color: red; font-size: 1.2em;", habits);
        return <p className="error-message">Критична помилка: дані звичок не є масивом.</p>;
    }

    const activeHabits = habits.filter(h => h && !h.isArchived);
    const archivedHabits = habits.filter(h => h && h.isArchived);

    return (
        <div className="habits-page-container">
            <div className="habits-header">
                <h1 className="page-title">Мої Звички</h1>
                <button onClick={handleOpenCreateModal} className="button-primary add-habit-btn">
                    + Додати звичку
                </button>
            </div>

            {pageError && !isModalOpen && <p className="error-message">{pageError}</p>}

            <h3>Активні звички</h3>
            {isLoadingPage && <p>Оновлення списку...</p>}
            {!isLoadingPage && activeHabits.length === 0 && !pageError && (
                <p>У вас поки немає активних звичок. <Link to="#" onClick={handleOpenCreateModal}>Створити першу?</Link></p>
            )}
            <div className="habits-list">
                {activeHabits.map(habit => {
                    if (!habit || typeof habit.id === 'undefined') {
                        console.warn("HabitsPage: Rendering active habit item with invalid data:", habit);
                        return null;
                    }
                    return (
                        <HabitListItem
                            key={habit.id}
                            habit={habit}
                            onEdit={handleOpenEditModal}
                            onDelete={handleDeleteHabit}
                            onArchiveToggle={handleArchiveToggle}
                        />
                    );
                })}
            </div>

            {archivedHabits.length > 0 && (
                <>
                    <h3 style={{marginTop: '30px'}}>Архівні звички</h3>
                    <div className="habits-list archived">
                        {archivedHabits.map(habit => {
                            if (!habit || typeof habit.id === 'undefined') {
                                console.warn("HabitsPage: Rendering archived habit item with invalid data:", habit);
                                return null;
                            }
                            return (
                                <HabitListItem
                                    key={habit.id}
                                    habit={habit}
                                    onEdit={handleOpenEditModal}
                                    onDelete={handleDeleteHabit}
                                    onArchiveToggle={handleArchiveToggle}
                                />
                            );
                        })}
                    </div>
                </>
            )}

            {isModalOpen && (
                <Modal onClose={handleCloseModal} title={editingHabit ? "Редагувати звичку" : "Нова звичка"}>
                    <HabitForm
                        onSubmit={handleFormSubmit}
                        initialData={editingHabit}
                        onCancel={handleCloseModal}
                        isLoading={formIsLoading}
                    />
                    {formError && <p className="error-message" style={{marginTop: '15px'}}>{formError}</p>}
                </Modal>
            )}
        </div>
    );
};

export default HabitsPage;