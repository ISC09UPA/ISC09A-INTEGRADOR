import '../global.css';

import AsyncStorage from '@react-native-async-storage/async-storage';
import {
  QueryClient,
  QueryClientProvider,
  focusManager,
} from '@tanstack/react-query';
import { Stack } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useColorScheme, vars } from 'nativewind';
import { useEffect, useState } from 'react';
import { AppState, Platform, View } from 'react-native';

import { ConfiguracionProvider } from '@/context/ConfiguracionContext';
import { colores } from '@/theme/tokens';

import { CLAVE_TEMA } from '../../services/configuracionService';
import { useSesion } from '../../store/useSesionStore';

// Mantener visible el Splash Screen hasta que sesión y tema estén listos.
SplashScreen.preventAutoHideAsync().catch(() => {});

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
});

// Convierte HEX (#FFFFFF) a RGB (255 255 255).
function hexToRgb(hex: string): string {
  const valor = hex.replace('#', '');

  const r = parseInt(valor.substring(0, 2), 16);
  const g = parseInt(valor.substring(2, 4), 16);
  const b = parseInt(valor.substring(4, 6), 16);

  return `${r} ${g} ${b}`;
}

export default function LayoutRaiz() {
  const { colorScheme, setColorScheme } = useColorScheme();

  const usuario = useSesion((s) => s.usuario);
  const inicializando = useSesion((s) => s.inicializando);
  const inicializar = useSesion((s) => s.inicializar);

  const [temaCargado, setTemaCargado] = useState(false);

  // Restaura la sesión guardada (SecureStore).
  useEffect(() => {
    void inicializar();
  }, [inicializar]);

  // Aplica el tema guardado antes de mostrar nada.
  useEffect(() => {
    AsyncStorage.getItem(CLAVE_TEMA)
      .then((guardado) => {
        if (
          guardado === 'light' ||
          guardado === 'dark' ||
          guardado === 'system'
        ) {
          setColorScheme(guardado);
        }
      })
      .catch(() => {})
      .finally(() => setTemaCargado(true));
  }, [setColorScheme]);

  // En React Native se utiliza el estado de la aplicación
  // para actualizar el enfoque de las consultas.
  useEffect(() => {
    const suscripcion = AppState.addEventListener(
      'change',
      (estado) => {
        if (Platform.OS !== 'web') {
          focusManager.setFocused(estado === 'active');
        }
      },
    );

    return () => suscripcion.remove();
  }, []);

  const listo = !inicializando && temaCargado;

  useEffect(() => {
    if (listo) {
      SplashScreen.hideAsync().catch(() => {});
    }
  }, [listo]);

  // Seleccionar los colores correspondientes al tema.
  const tema =
    colorScheme === 'dark' ? colores.oscuro : colores.claro;

  // Crear variables CSS automáticamente desde tokens.ts.
  const variablesTema = Object.fromEntries(
    Object.entries(tema).map(([nombre, valor]) => {
      const nombreCSS = nombre.replace(
        /[A-Z]/g,
        (letra) => `-${letra.toLowerCase()}`,
      );

      return [`--color-${nombreCSS}`, hexToRgb(valor)];
    }),
  ) as Record<`--${string}`, string>;

  // El splash sigue visible mientras tanto.
  if (!listo) return null;

  return (
    <QueryClientProvider client={queryClient}>
      <ConfiguracionProvider>
        <View style={[{ flex: 1 }, vars(variablesTema)]}>
          <StatusBar
            style={colorScheme === 'dark' ? 'light' : 'dark'}
          />

          <Stack screenOptions={{ headerShown: false }}>
            {/* Rutas privadas */}
            <Stack.Protected guard={!!usuario}>
              <Stack.Screen name="(tabs)" />
              <Stack.Screen name="opciones" />
              <Stack.Screen name="perfil" />
              <Stack.Screen name="admin/usuarios" />
              <Stack.Screen name="vivero-mapa" />
            </Stack.Protected>

            {/* Login: solo sin sesión */}
            <Stack.Protected guard={!usuario}>
              <Stack.Screen name="(auth)" />
            </Stack.Protected>
          </Stack>
        </View>
      </ConfiguracionProvider>
    </QueryClientProvider>
  );
}