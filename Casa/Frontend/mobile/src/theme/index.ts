/**
 * Design tokens for the "Casa" smart-home app.
 *
 * Visual direction: soft blue sky gradient, frosted-glass surfaces,
 * Manrope for display/headings and Inter for UI text.
 */

import { Platform } from 'react-native';

import { BottomTabInset, MaxContentWidth, Spacing } from '@/constants/theme';

export { BottomTabInset, MaxContentWidth, Spacing };

/** Core brand palette. */
export const Palette = {
  ink: '#142B3A',
  inkSoft: '#526879',
  inkMuted: '#526879',
  accent: '#087F83',
  accentSoft: '#D1F0E4',
  accentInk: '#006C72',
  success: '#126C59',
  successSoft: '#D1F0E4',
  warning: '#805B13',
  warningSoft: '#FFF0CD',
  danger: '#9C3D36',
  dangerSoft: '#FBE0DF',
  neutral: '#65747B',
  neutralSoft: '#DEE7F0',
  white: '#FFFFFF',
} as const;

/** Screen background gradient stops (top → bottom). */
export const Gradients = {
  screen: ['#D9EBF4', '#E9EDF7', '#D9E8EE'] as const,
  accent: ['#26A0A1', '#087F83'] as const,
  night: ['#274257', '#12263A'] as const,
};

/** Frosted-glass surface recipe. */
export const Glass = {
  fill: 'rgba(255, 255, 255, 0.44)',
  fillStrong: 'rgba(255, 255, 255, 0.52)',
  fillSubtle: 'rgba(255, 255, 255, 0.38)',
  border: 'rgba(255, 255, 255, 0.85)',
  borderSubtle: 'rgba(255, 255, 255, 0.45)',
  shadowColor: '#294E71',
  shadowOpacity: 0.08,
  elevation: 3,
  blurIntensity: 40,
} as const;

export const Radii = {
  sm: 10,
  md: 16,
  lg: 20,
  xl: 24,
  pill: 999,
} as const;

/** Font families loaded via @expo-google-fonts (see hooks/use-app-fonts). */
export const FontFamily = {
  /** Manrope — display and headings. */
  displayBold: 'Manrope_700Bold',
  displaySemiBold: 'Manrope_600SemiBold',
  /** Inter — body and UI. */
  body: 'Inter_400Regular',
  bodyMedium: 'Inter_500Medium',
  bodySemiBold: 'Inter_600SemiBold',
  fallback: Platform.select({ ios: 'System', default: 'sans-serif' }) ?? 'sans-serif',
} as const;

export const FontSize = {
  caption: 12,
  footnote: 13,
  body: 15,
  callout: 16,
  headline: 17,
  title: 20,
  titleLarge: 24,
  display: 30,
  displayLarge: 36,
} as const;

export const LineHeight = {
  tight: 1.2,
  snug: 1.35,
  relaxed: 1.5,
} as const;

/** Semantic tones shared by banners, chips, metrics, … */
export type Tone = 'neutral' | 'accent' | 'success' | 'warning' | 'danger';

export const ToneColors: Record<Tone, { fg: string; bg: string }> = {
  neutral: { fg: Palette.inkSoft, bg: Palette.neutralSoft },
  accent: { fg: Palette.accentInk, bg: Palette.accentSoft },
  success: { fg: '#126C59', bg: Palette.successSoft },
  warning: { fg: '#805B13', bg: Palette.warningSoft },
  danger: { fg: '#9C3D36', bg: Palette.dangerSoft },
};

export const ZIndex = {
  tabbar: 20,
  overlay: 50,
} as const;
