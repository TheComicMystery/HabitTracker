import React, { createContext, useContext, useState } from 'react';

const MobileContext = createContext();

export const useMobile = () => useContext(MobileContext);

export const MobileProvider = ({ children }) => {
    const [isMobileView, setIsMobileView] = useState(false);

    const toggleMobileView = () => {
        setIsMobileView((prev) => !prev);
    };

    return (
        <MobileContext.Provider value={{ isMobileView, toggleMobileView }}>
            {children}
        </MobileContext.Provider>
    );
};