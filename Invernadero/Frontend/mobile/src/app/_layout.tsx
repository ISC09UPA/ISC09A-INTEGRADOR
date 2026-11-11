
import '../global.css';

import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';
import { View } from 'react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useColorScheme, vars } from 'nativewind';

import { colores } from '@/theme/tokens';

// Mantener visible el Splash Screen hasta iniciar.
SplashScreen.preventAutoHideAsync().catch(() => {});

const queryClient = new QueryClient();

// Convierte HEX (#FFFFFF) a RGB (255 255 255).
function hexToRgb(hex: string): string {
  const valor = hex.replace('#', '');

  const r = parseInt(valor.substring(0, 2), 16);
  const g = parseInt(valor.substring(2, 4), 16);
  const b = parseInt(valor.substring(4, 6), 16);

  return `${r} ${g} ${b}`;
}

export default function LayoutRaiz() {
  const { colorScheme } = useColorScheme();

  useEffect(() => {
    SplashScreen.hideAsync().catch(() => {});
  }, []);

  // Seleccionar los colores correspondientes al tema.
  const tema = colorScheme === 'dark'
    ? colores.oscuro
    : colores.claro;

  // Crear variables CSS automáticamente desde tokens.ts.
  const variablesTema = Object.fromEntries(
    Object.entries(tema).map(([nombre, valor]) => {
      const nombreCSS = nombre.replace(
        /[A-Z]/g,
        letra => `-${letra.toLowerCase()}`
      );

      return [`--color-${nombreCSS}`, hexToRgb(valor)];
    })
  ) as Record<`--${string}`, string>;

  return (
    <QueryClientProvider client={queryClient}>
      <View style={[{ flex: 1 }, vars(variablesTema)]}>
        <StatusBar
          style={colorScheme === 'dark' ? 'light' : 'dark'}
        />

        <Stack screenOptions={{ headerShown: false }}>
          <Stack.Screen name="(tabs)" />
        </Stack>
      </View>
    </QueryClientProvider>
  );
}
