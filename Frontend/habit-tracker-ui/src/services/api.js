import axios from 'axios';

const normalizeApiUrl = (url) => url?.replace(/\/+$/, '');
const currentHost = window.location.hostname || 'localhost';
const configuredApiUrl = normalizeApiUrl(import.meta.env.VITE_API_URL);
const apiCandidates = [
    configuredApiUrl,
    `${window.location.protocol}//${currentHost}:5097/api`,
    `${window.location.protocol}//${currentHost}:5088/api`,
    `${window.location.protocol}//${currentHost}:5080/api`,
]
    .filter(Boolean)
    .filter((url, index, urls) => urls.indexOf(url) === index);

const API_URL = apiCandidates[0];

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

apiClient.interceptors.response.use(
    (response) => response,
    async (error) => {
        const config = error.config;
        const isConnectionError = !error.response && config && !config.__apiFallbackTried;
        if (!isConnectionError) return Promise.reject(error);

        const currentBaseUrl = normalizeApiUrl(config.baseURL || apiClient.defaults.baseURL);
        const currentIndex = apiCandidates.indexOf(currentBaseUrl);
        const fallbackUrls = apiCandidates.slice(currentIndex + 1);

        for (const fallbackUrl of fallbackUrls) {
            try {
                config.__apiFallbackTried = true;
                config.baseURL = fallbackUrl;
                apiClient.defaults.baseURL = fallbackUrl;
                return await apiClient.request(config);
            } catch (fallbackError) {
                if (fallbackError.response) return Promise.reject(fallbackError);
            }
        }

        return Promise.reject(error);
    }
);

export const registerUser = (userData) => apiClient.post('/auth/register', userData);
export const loginUser = (userData) => apiClient.post('/auth/login', userData);
export const getOnboardingProfile = () => apiClient.get('/auth/onboarding');
export const updateOnboardingProfile = (profileData) => apiClient.put('/auth/onboarding', profileData);

export const getHabits = (includeArchived = false, includePredictions = true) => apiClient.get(`/habits?includeArchived=${includeArchived}&includePredictions=${includePredictions}`);
export const createHabit = (habitData) => apiClient.post('/habits', habitData);
export const updateHabit = (habitId, habitData) => apiClient.put(`/habits/${habitId}`, habitData);
export const deleteHabit = (habitId) => apiClient.delete(`/habits/${habitId}`);
export const trackHabit = (habitId, trackData) => apiClient.post(`/habits/${habitId}/track`, trackData);
export const getDailyHabitsStatus = (date) => apiClient.get(`/habits/daily?date=${date}`); 
export const getHabitEntries = (habitId, startDate, endDate) => apiClient.get(`/habits/${habitId}/entries?startDate=${startDate}&endDate=${endDate}`);
export const getAllHabitEntries = (startDate, endDate) => apiClient.get(`/habits/entries?startDate=${startDate}&endDate=${endDate}`);
export const logHabitConfidence = (habitId, confidenceData) => apiClient.post(`/habits/${habitId}/confidence`, confidenceData);
export const getCategories = () => apiClient.get('/habits/categories');
export const createCategory = (categoryData) => apiClient.post('/habits/categories', categoryData);
export const updateCategory = (categoryId, categoryData) => apiClient.put(`/habits/categories/${categoryId}`, categoryData);
export const deleteCategory = (categoryId) => apiClient.delete(`/habits/categories/${categoryId}`);
export const createPomodoroSession = (sessionData) => apiClient.post('/habits/pomodoro-sessions', sessionData);
export const getPomodoroSessions = (startDate, endDate) => apiClient.get(`/habits/pomodoro-sessions?startDate=${startDate}&endDate=${endDate}`);
export const getPomodoroStats = (startDate, endDate) => apiClient.get(`/habits/pomodoro-stats?startDate=${startDate}&endDate=${endDate}`);

export default apiClient;
