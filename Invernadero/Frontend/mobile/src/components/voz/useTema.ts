import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

/** Tokens de color del tema actual (claro/oscuro) desde theme/tokens.ts. */
export function useTema() {
  const { colorScheme } = useColorScheme();
  return colorScheme === 'dark' ? colores.oscuro : colores.claro;
}
