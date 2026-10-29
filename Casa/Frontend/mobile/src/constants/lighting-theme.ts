/**
 * Colores, tamaños y tipografía de la pantalla de Iluminación (estilo glass).
 * Se mantienen separados de `theme.ts` para no modificar el tema base del proyecto.
 */

import { Platform, type TextStyle, type ViewStyle } from 'react-native';

export const LightingColors = {
  background: '#EEF2F7',
  backgroundTop: '#F3F6FA',
  backgroundBottom: '#E8EDF3',
  blobTeal: '#A9D8E6',
  blobLavender: '#C9C6EE',

  accent: '#1F6F7A',
  navy: '#14283C',
  muted: '#5B6B7A',
  success: '#1E7A55',
  white: '#FFFFFF',

  glass: 'rgba(255,255,255,0.55)',
  glassBorder: 'rgba(255,255,255,0.9)',

  selectedBg: '#D5ECE6',
  selectedText: '#1E7F78',

  disabledBg: '#AEBBC4',
  disabledText: '#33434F',

  warningBg: '#F6EBD3',
  warningText: '#8A5A12',

  shadow: '#14283C',
} as const;

export const LightingRadii = {
  banner: 24,
  card: 28,
} as const;

export const LightingSizes = {
  borderWidth: 1.5,
  segmentHeight: 64,
  actionHeight: 72,
  headerIcon: 20,
} as const;

export const LightingTypography = {
  eyebrow: {
    fontSize: 15,
    fontWeight: '600',
    letterSpacing: 1.5,
    textTransform: 'uppercase',
    color: LightingColors.accent,
  },
  title: {
    fontSize: 40,
    lineHeight: 48,
    fontWeight: '500',
    color: LightingColors.navy,
  },
  subtitle: {
    fontSize: 16,
    lineHeight: 22,
    color: LightingColors.muted,
  },
  label: {
    fontSize: 14,
    letterSpacing: 0.5,
    textTransform: 'uppercase',
    color: LightingColors.muted,
  },
  metric: {
    fontSize: 48,
    lineHeight: 58,
    fontWeight: '400',
    color: LightingColors.navy,
  },
  status: {
    fontSize: 30,
    lineHeight: 38,
    fontWeight: '400',
    color: LightingColors.navy,
  },
  body: {
    fontSize: 16,
    lineHeight: 22,
  },
  caption: {
    fontSize: 14,
    lineHeight: 20,
    color: LightingColors.muted,
  },
  button: {
    fontSize: 17,
    fontWeight: '500',
  },
} satisfies Record<string, TextStyle>;

export const LightingShadow: ViewStyle =
  Platform.select<ViewStyle>({
    ios: {
      shadowColor: LightingColors.shadow,
      shadowOffset: { width: 0, height: 8 },
      shadowOpacity: 0.06,
      shadowRadius: 20,
    },
    android: {
      elevation: 2,
      shadowColor: LightingColors.shadow,
    },
    default: {
      boxShadow: '0px 8px 20px rgba(20,40,60,0.06)',
    },
  }) ?? {};

export const GlassSurface: ViewStyle = {
  backgroundColor: LightingColors.glass,
  borderColor: LightingColors.glassBorder,
  borderWidth: LightingSizes.borderWidth,
  borderRadius: LightingRadii.card,
  ...LightingShadow,
};
