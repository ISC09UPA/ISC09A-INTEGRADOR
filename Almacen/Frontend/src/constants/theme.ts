/**
 * Paleta y medidas de la app. Son las mismas del tema de Blazor (Components/Layout/Tema.cs),
 * para que la web y el teléfono se vean como un solo sistema.
 */

export const Colors = {
  light: {
    primary: '#1F5F8B',
    onPrimary: '#FFFFFF',
    secondary: '#5B6B7A',
    tertiary: '#2E8B57',
    success: '#2E7D32',
    warning: '#B26A00',
    error: '#C62828',
    background: '#F4F6F8',
    surface: '#FFFFFF',
    text: '#1B2733',
    textSecondary: '#5B6B7A',
    line: '#E1E6EB',
    drawer: '#14324A',
    drawerText: '#DCE6EF',
    drawerIcon: '#9FB6C9',
  },
  dark: {
    primary: '#6BA8D6',
    onPrimary: '#0E151B',
    secondary: '#9AA9B7',
    tertiary: '#5CC08A',
    success: '#66BB6A',
    warning: '#FFB74D',
    error: '#EF5350',
    background: '#0E151B',
    surface: '#18222C',
    text: '#E6EDF3',
    textSecondary: '#9AA9B7',
    line: '#2A3642',
    drawer: '#111A22',
    drawerText: '#DCE6EF',
    drawerIcon: '#9FB6C9',
  },
} as const;

export type Paleta = { [K in keyof typeof Colors.light]: string };

export const Spacing = {
  half: 2,
  one: 4,
  two: 8,
  three: 16,
  four: 24,
  five: 32,
  six: 64,
} as const;

export const Radio = 8;
export const MaxContentWidth = 900;
