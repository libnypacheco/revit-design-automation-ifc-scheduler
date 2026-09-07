import {loadTheme} from 'office-ui-fabric-react';

/**
 * Brand palette.
 *
 * These are the brand hexes as issued. Most of them are large-surface colours:
 * none of them reaches the WCAG AA 4.5:1 contrast ratio against white text
 * (Teal, the darkest, manages 3.26:1), so they are used here for fills,
 * accents and borders rather than behind or as text.
 */
export const brand = {
    mint: '#89D0C8',
    teal: '#389BAA',
    denim: '#A1B2BF',
    darkDenim: '#6A88A0',
    pink: '#F49AC1',
    purple: '#B586A5',
    mistyGreen: '#C5D5CB',
    forrest: '#6C8C78',
    sand: '#B0A89B',
    slate: '#7A9193',
    grey: '#777777',
    white: '#ffffff',
    black: '#000000',
} as const;

/**
 * Text-bearing shades derived from the palette.
 *
 * Anything that carries text — the header bar, primary buttons, links, inline
 * code — needs 4.5:1. These are the brand hues darkened until they clear it,
 * so the brand reads the same while the text stays legible.
 */
export const brandText = {
    /** brand.teal darkened 25%: 5.37:1 on white, 5.37:1 behind white text */
    teal: '#2a7480',
    /** brand.purple darkened 30%: 5.60:1 on white, for inline code */
    purple: '#7f5e73',
} as const;

/**
 * Fluent brand ramp generated from brandText.teal using the Fluent Theme
 * Designer's tint/shade proportions.
 *
 * Only the theme (brand) slots are overridden. Fluent's neutral ramp is left
 * at its defaults, because those greys are already tuned for text contrast and
 * replacing them with brand greys would regress it.
 */
export const fluentTheme = {
    palette: {
        themePrimary: '#2a7480',
        themeLighterAlt: '#f4f8f9',
        themeLighter: '#dfeaec',
        themeLight: '#bfd5d9',
        themeTertiary: '#7facb3',
        themeSecondary: '#44858f',
        themeDarkAlt: '#276c77',
        themeDark: '#215c65',
        themeDarker: '#184148',
    },
};

export function applyBrandTheme(): void {
    loadTheme(fluentTheme);
}
