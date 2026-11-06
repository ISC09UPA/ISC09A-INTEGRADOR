import type { LockerEstado } from '@/types';

export const colors = {
  fondo: '#F8F7F3',
  superficie: '#FFFFFF',
  borde: '#E8E6DF',
  verdeOscuro: '#1B3D2B',
  verdeClaro: '#B9E4C0',
  verdeSuave: '#DCEFDF',
  texto: '#14201A',
  textoSuave: '#6B746E',
  gris: '#EFEEE9',
  disponible: '#5DB075',
  ocupado: '#C9A45C',
  mantenimiento: '#9AA39D',
  alerta: '#D1495B',
};

export const radius = { sm: 12, md: 16, lg: 24, pill: 999 };

export const font = {
  regular: 'DMSans_400Regular',
  medium: 'DMSans_500Medium',
  bold: 'DMSans_700Bold',
};

export const estadoColor: Record<LockerEstado, string> = {
  Disponible: colors.disponible,
  Ocupado: colors.ocupado,
  Mantenimiento: colors.mantenimiento,
};
