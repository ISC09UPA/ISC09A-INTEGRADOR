import { palettes } from '@/constants/casa-theme';
import { useColorScheme } from './use-color-scheme';

/** Paleta de colores del esquema actual (claro u oscuro). */
export function useCasaTheme() {
  return palettes[useColorScheme() === 'dark' ? 'dark' : 'light'];
}
