export const colores = {
  claro: {
    surface: '#F6F8F5',
    surfaceDim: '#EBEFE9',
    surfaceBright: '#FFFFFF',
    surfaceContainerLowest: '#FFFFFF',
    surfaceContainerLow: '#F1F4EE',
    surfaceContainer: '#E8ECE4',
    surfaceContainerHigh: '#E0E6DB',
    surfaceContainerHighest: '#D7DFD1',

    primary: '#2E7D32',
    primaryContainer: '#C8E6C9',
    onPrimary: '#FFFFFF',
    onPrimaryContainer: '#1B5E20',

    secondary: '#1E88E5',
    secondaryContainer: '#BBDEFB',
    onSecondary: '#FFFFFF',
    onSecondaryContainer: '#0D47A1',

    tertiary: '#FB8C00',
    tertiaryContainer: '#FFE0B2',
    onTertiary: '#FFFFFF',
    onTertiaryContainer: '#E65100',

    error: '#E53935',
    errorContainer: '#FFCDD2',
    onError: '#FFFFFF',
    onErrorContainer: '#B71C1C',

    outline: '#81C784',
    outlineVariant: '#E0E0E0',
    onSurface: '#1B1B1B',
    onSurfaceVariant: '#555555',

    inverseSurface: '#2d322b',
    inverseOnSurface: '#eef2e8',
    surfaceTint: '#1b6d24',
    inversePrimary: '#88d982',

    primaryFixed: '#a3f69c',
    primaryFixedDim: '#88d982',
    onPrimaryFixed: '#002204',
    onPrimaryFixedVariant: '#005312',

    background: '#f7fbf0',
    onBackground: '#181d17',
    surfaceVariant: '#e0e4da',
  },

  oscuro: {
    surface: '#121712',
    surfaceDim: '#101510',
    surfaceBright: '#353D35',
    surfaceContainerLowest: '#0D120D',
    surfaceContainerLow: '#1A211A',
    surfaceContainer: '#202820',
    surfaceContainerHigh: '#293329',
    surfaceContainerHighest: '#343F34',

    primary: '#81C784',
    primaryContainer: '#1B5E20',
    onPrimary: '#003909',
    onPrimaryContainer: '#C8E6C9',

    secondary: '#90CAF9',
    secondaryContainer: '#0D47A1',
    onSecondary: '#003258',
    onSecondaryContainer: '#BBDEFB',

    tertiary: '#FFB74D',
    tertiaryContainer: '#E65100',
    onTertiary: '#482900',
    onTertiaryContainer: '#FFE0B2',

    error: '#EF9A9A',
    errorContainer: '#B71C1C',
    onError: '#690005',
    onErrorContainer: '#FFCDD2',

    outline: '#558B5B',
    outlineVariant: '#454F45',
    onSurface: '#E2E8DF',
    onSurfaceVariant: '#BEC9BA',

    inverseSurface: '#E2E8DF',
    inverseOnSurface: '#2D322B',
    surfaceTint: '#81C784',
    inversePrimary: '#2E7D32',

    primaryFixed: '#C8E6C9',
    primaryFixedDim: '#81C784',
    onPrimaryFixed: '#002204',
    onPrimaryFixedVariant: '#1B5E20',

    background: '#121712',
    onBackground: '#E2E8DF',
    surfaceVariant: '#343F34',
  },
} as const;

export type NombreTema = keyof typeof colores;