import { Colors, type Paleta } from '@/constants/theme';
import { useColorScheme } from '@/hooks/use-color-scheme';

/** Colores del tema claro u oscuro según el sistema. */
export function useColores(): Paleta {
  return useColorScheme() === 'dark' ? Colors.dark : Colors.light;
}
