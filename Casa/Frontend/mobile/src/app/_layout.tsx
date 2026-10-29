import { DefaultTheme, Stack, ThemeProvider } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';

import { AnimatedSplashOverlay } from '@/components/animated-icon';
import { useAppFonts } from '@/hooks/use-app-fonts';

SplashScreen.preventAutoHideAsync();

export default function RootLayout() {
  const fontsReady = useAppFonts();

  // Keep the native splash visible until the Manrope/Inter fonts are ready.
  if (!fontsReady) return null;

  return (
    <ThemeProvider value={DefaultTheme}>
      <AnimatedSplashOverlay />
      <Stack screenOptions={{ headerShown: false }}>
        <Stack.Screen name="(tabs)" />
        <Stack.Screen name="catalog" />
      </Stack>
    </ThemeProvider>
  );
}
