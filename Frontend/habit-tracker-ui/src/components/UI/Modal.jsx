import React, { useEffect } from 'react';
import './Modal.css';

const Modal = ({ children, onClose, title }) => {
    const handleBackgroundClick = (e) => {
        if (e.target === e.currentTarget) {
            onClose();
        }
    };

    useEffect(() => {
        const handleEscKey = (event) => {
            if (event.key === 'Escape') {
                onClose();
            }
        };

        document.addEventListener('keydown', handleEscKey);

        return () => {
            document.removeEventListener('keydown', handleEscKey);
        };
    }, [onClose]);

    return (
        <div className="modal-backdrop" onClick={handleBackgroundClick} role="dialog" aria-modal="true" aria-labelledby="modal-title-id">
            <div className="modal-content">
                <div className="modal-header">
                    {title && <h3 className="modal-title" id="modal-title-id">{title}</h3>}
                    <button 
                        onClick={onClose} 
                        className="modal-close-button" 
                        title="Закрити"
                        aria-label="Закрити модальне вікно"
                    >
                        ×
                    </button>
                </div>
                <div className="modal-body">
                    {children}
                </div>
            </div>
        </div>
    );
};

export default Modal;