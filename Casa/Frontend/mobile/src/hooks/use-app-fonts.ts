import {
  Inter_400Regular,
  Inter_500Medium,
  Inter_600SemiBold,
  useFonts,
} from '@expo-google-fonts/inter';
import {
  Manrope_600SemiBold,
  Manrope_700Bold,
  Manrope_800ExtraBold,
} from '@expo-google-fonts/manrope';

/**
 * Loads the app typography (Manrope + Inter).
 * Returns `true` once fonts are loaded or if loading failed, so the app
 * never gets stuck on an empty screen because of a font issue.
 */
export function useAppFonts(): boolean {
  const [loaded, error] = useFonts({
    Manrope_600SemiBold,
    Manrope_700Bold,
    Manrope_800ExtraBold,
    Inter_400Regular,
    Inter_500Medium,
    Inter_600SemiBold,
  });

  return loaded || error != null;
}
