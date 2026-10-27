import React, { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { getOnboardingProfile, updateOnboardingProfile } from '../../services/api';
import './SmartOnboarding.css';

const timeOptions = [
  { value: 'morning', label: 'Ранок' },
  { value: 'day', label: 'День' },
  { value: 'evening', label: 'Вечір' },
  { value: 'flexible', label: 'Неважливо' }
];

const reasonOptions = [
  { value: 'time', label: 'Брак часу' },
  { value: 'energy', label: 'Втома' },
  { value: 'mood', label: 'Настрій' },
  { value: 'motivation', label: 'Прокрастинація' },
  { value: 'health', label: "Здоров'я" },
  { value: 'events', label: 'Свята / гості' },
  { value: 'forgetting', label: 'Забуваю' }
];

const SmartOnboarding = ({ isAuthenticated }) => {
  const [visible, setVisible] = useState(false);
  const [step, setStep] = useState(0);
  const [saving, setSaving] = useState(false);
  const [profile, setProfile] = useState({
    subjectiveDifficulty: 5,
    overallDisciplineSelf: 5,
    weekendVulnerability: 2,
    preferredTimeOfDay: 'flexible',
    initialFailureReason: 'time'
  });

  useEffect(() => {
    if (!isAuthenticated) return;
    let cancelled = false;
    getOnboardingProfile()
      .then(({ data }) => {
        if (!cancelled && !data?.onboardingCompleted) setVisible(true);
      })
      .catch(() => {});
    return () => { cancelled = true; };
  }, [isAuthenticated]);

  const setField = (field, value) => {
    setProfile(current => ({ ...current, [field]: value }));
  };

  const close = async (complete = true) => {
    setSaving(true);
    try {
      await updateOnboardingProfile({ ...profile, complete });
      setVisible(false);
    } finally {
      setSaving(false);
    }
  };

  if (!visible) return null;

  const content = (
    <div className="onboarding-backdrop">
      <section className="onboarding-card" aria-label="Налаштування персонального прогнозу">
        <div className="onboarding-head">
          <span className="onboarding-kicker">Персоналізація</span>
          <button type="button" className="onboarding-skip" onClick={() => close(false)} disabled={saving}>
            Пропустити
          </button>
        </div>

        {step === 0 && (
          <div className="onboarding-step">
            <h2>Підлаштуємо прогнози під ваш ритм</h2>
            <p>Три короткі відповіді допоможуть моделі краще розуміти стартові ризики без зайвих запитань.</p>
            <label className="range-field">
              <span>Наскільки складно вам зазвичай втримувати нові звички?</span>
              <strong>{profile.subjectiveDifficulty}/10</strong>
              <input type="range" min="1" max="10" value={profile.subjectiveDifficulty} onChange={(e) => setField('subjectiveDifficulty', Number(e.target.value))} />
            </label>
            <label className="range-field">
              <span>Як ви оцінюєте свою дисципліну зараз?</span>
              <strong>{profile.overallDisciplineSelf}/10</strong>
              <input type="range" min="1" max="10" value={profile.overallDisciplineSelf} onChange={(e) => setField('overallDisciplineSelf', Number(e.target.value))} />
            </label>
          </div>
        )}

        {step === 1 && (
          <div className="onboarding-step">
            <h2>Коли звички даються легше?</h2>
            <p>Це допомагає відрізняти природний режим від випадкових збоїв у графіку.</p>
            <div className="choice-grid">
              {timeOptions.map(option => (
                <button key={option.value} type="button" className={profile.preferredTimeOfDay === option.value ? 'selected' : ''} onClick={() => setField('preferredTimeOfDay', option.value)}>
                  {option.label}
                </button>
              ))}
            </div>
            <label className="range-field compact">
              <span>Наскільки вихідні або свята збивають вас?</span>
              <strong>{profile.weekendVulnerability}/4</strong>
              <input type="range" min="1" max="4" value={profile.weekendVulnerability} onChange={(e) => setField('weekendVulnerability', Number(e.target.value))} />
            </label>
          </div>
        )}

        {step === 2 && (
          <div className="onboarding-step">
            <h2>Що найчастіше заважає?</h2>
            <p>Оберіть одну головну причину. Деталі можна буде уточнювати пізніше лише тоді, коли модель сумнівається.</p>
            <div className="choice-grid reason-grid">
              {reasonOptions.map(option => (
                <button key={option.value} type="button" className={profile.initialFailureReason === option.value ? 'selected' : ''} onClick={() => setField('initialFailureReason', option.value)}>
                  {option.label}
                </button>
              ))}
            </div>
          </div>
        )}

        <div className="onboarding-actions">
          <div className="onboarding-dots">
            {[0, 1, 2].map(index => <span key={index} className={index === step ? 'active' : ''} />)}
          </div>
          <div className="onboarding-buttons">
            {step > 0 && <button type="button" className="button button-secondary" onClick={() => setStep(step - 1)} disabled={saving}>Назад</button>}
            {step < 2 ? (
              <button type="button" className="button button-primary" onClick={() => setStep(step + 1)}>Далі</button>
            ) : (
              <button type="button" className="button button-primary" onClick={() => close(true)} disabled={saving}>
                {saving ? 'Збереження...' : 'Готово'}
              </button>
            )}
          </div>
        </div>
      </section>
    </div>
  );

  return createPortal(content, document.body);
};

export default SmartOnboarding;
