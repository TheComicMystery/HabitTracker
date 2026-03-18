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

    useEffect(() => {
        fetchHabits();
    }, [fetchHabits]);

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

    const activeHabits = habits ? habits.filter(h => h && !h.isArchived) : [];
    const archivedHabits = habits ? habits.filter(h => h && h.isArchived) : [];

    return (
        <div className="habits-page-container">
            <div className="habits-header">
                <h1 className="page-title">Мої Звички</h1>
                <button onClick={handleOpenCreateModal} className="button button-primary add-habit-btn">
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