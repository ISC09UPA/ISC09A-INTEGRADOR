/**
 * Design tokens based on the approved visual system:
 * marfil background, navy text, teal actions.
 */
export const tokens = {
  colors: {
    background: '#F8F6EF',
    surface: '#FFFDF8',
    primaryText: '#142E40',
    secondaryText: '#586971',
    action: '#087F78',
    confirmation: '#E2F1EB',
    border: '#DEDFD7',
    pendingBackground: '#FFF0D0',
    pendingText: '#805008',
    errorBackground: '#FCE9E4',
    errorText: '#A23427',
  },
  spacing: {
    xs: 4,
    sm: 8,
    md: 16,
    lg: 24,
    xl: 32,
  },
  borderRadius: {
    card: 20,
    button: 13,
  },
  typography: {
    title: 28,
    body: 16,
    small: 13,
    caption: 11,
  },
} as const;
