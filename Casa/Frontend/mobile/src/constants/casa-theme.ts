import type { ComponentProps } from 'react';
import type { TextStyle, ViewStyle } from 'react-native';
import type { Feather } from '@expo/vector-icons';

export const colors = {
  ink: '#16222E',
  muted: '#5F6B77',
  faint: '#8A95A0',
  teal: '#1F7F78',
  green: '#12704F',
  white: '#FFFFFF',
  greenSoft: 'rgba(196, 234, 220, 0.85)',
  glass: 'rgba(255, 255, 255, 0.55)',
  glassBorder: 'rgba(255, 255, 255, 0.95)',
} as const;

export const fonts = {
  regular: 'Inter_400Regular',
  medium: 'Inter_500Medium',
  semibold: 'Inter_600SemiBold',
} as const;

export type Scheme = 'light' | 'dark';
export type Palette = typeof colors;

/** Paleta por esquema. El modo oscuro aún no tiene diseño propio (app.json fija el modo claro), así que reutiliza la clara. */
export const palettes: Record<Scheme, Palette> = { light: colors, dark: colors };

export type Color = keyof Palette;
export type Weight = keyof typeof fonts;
/** Solo acepta nombres de íconos que existen en Feather. */
export type IconName = ComponentProps<typeof Feather>['name'];

/** Estilo de texto base del tema: peso, tamaño y color. */
export const text = (weight: Weight, fontSize: number, color: Color = 'ink'): TextStyle => ({
  fontFamily: fonts[weight],
  fontSize,
  color: colors[color],
});

/** Superficie translúcida con borde y sombra, compartida por tarjetas y barra de pestañas. */
export const glass = (backgroundColor: string = colors.glass, shadowOpacity = 0.1): ViewStyle => ({
  backgroundColor,
  borderWidth: 1.5,
  borderColor: colors.glassBorder,
  shadowColor: '#5A6A8A',
  shadowOpacity,
  shadowRadius: 18,
  shadowOffset: { width: 0, height: 8 },
  elevation: 2,
});
