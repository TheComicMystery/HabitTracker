import axios from 'axios';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';

const apiClient = axios.create({
    baseURL: API_URL,
    headers: {
        'Content-Type': 'application/json',
    },
});

apiClient.interceptors.request.use(
    (config) => {
        const token = localStorage.getItem('authToken');
        if (token) {
            config.headers['Authorization'] = `Bearer ${token}`;
        }
        return config;
    },
    (error) => {
        return Promise.reject(error);
    }
);

export const registerUser = (userData) => apiClient.post('/auth/register', userData);
export const loginUser = (userData) => apiClient.post('/auth/login', userData);

export const getHabits = (includeArchived = false) => apiClient.get(`/habits?includeArchived=${includeArchived}`);
export const createHabit = (habitData) => apiClient.post('/habits', habitData);
export const updateHabit = (habitId, habitData) => apiClient.put(`/habits/${habitId}`, habitData);
export const deleteHabit = (habitId) => apiClient.delete(`/habits/${habitId}`);
export const trackHabit = (habitId, trackData) => apiClient.post(`/habits/${habitId}/track`, trackData);
export const getDailyHabitsStatus = (date) => apiClient.get(`/habits/daily?date=${date}`); 
export const getHabitEntries = (habitId, startDate, endDate) => apiClient.get(`/habits/${habitId}/entries?startDate=${startDate}&endDate=${endDate}`);
export const logHabitConfidence = (habitId, confidenceData) => apiClient.post(`/habits/${habitId}/confidence`, confidenceData);

export default apiClient;