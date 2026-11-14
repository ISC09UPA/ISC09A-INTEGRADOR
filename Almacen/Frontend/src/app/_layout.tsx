import { DarkTheme, DefaultTheme, Stack, ThemeProvider } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';
import { useColorScheme } from 'react-native';

import { Colors } from '@/constants/theme';
import { ProveedorSesion, useSesion } from '@/lib/sesion';

SplashScreen.preventAutoHideAsync();

export default function Raiz() {
  return (
    <ProveedorSesion>
      <Navegacion />
    </ProveedorSesion>
  );
}

function Navegacion() {
  const esquema = useColorScheme();
  const c = esquema === 'dark' ? Colors.dark : Colors.light;
  const base = esquema === 'dark' ? DarkTheme : DefaultTheme;
  const { sesion, cargando } = useSesion();

  useEffect(() => {
    if (!cargando) SplashScreen.hideAsync();
  }, [cargando]);

  if (cargando) return null;

  return (
    <ThemeProvider value={{ ...base, colors: { ...base.colors, primary: c.primary, background: c.background, card: c.surface, text: c.text, border: c.line } }}>
      <StatusBar style={esquema === 'dark' ? 'light' : 'dark'} />
      <Stack
        screenOptions={{
          headerStyle: { backgroundColor: c.surface },
          headerTintColor: c.text,
          headerTitleStyle: { fontWeight: '600' },
          contentStyle: { backgroundColor: c.background },
          headerBackButtonDisplayMode: 'minimal',
        }}>
        <Stack.Protected guard={!sesion}>
          <Stack.Screen name="ingresar" options={{ headerShown: false }} />
        </Stack.Protected>
        <Stack.Protected guard={!!sesion}>
          <Stack.Screen name="index" options={{ title: 'Almacén inteligente' }} />
          <Stack.Screen name="ventas/index" options={{ title: 'Ventas' }} />
          <Stack.Screen name="ventas/nueva" options={{ title: 'Nueva venta' }} />
          <Stack.Screen name="ventas/[id]" options={{ title: 'Venta' }} />
          <Stack.Screen name="recepciones/index" options={{ title: 'Recepciones' }} />
          <Stack.Screen name="recepciones/nueva" options={{ title: 'Nueva recepción' }} />
          <Stack.Screen name="recepciones/[id]" options={{ title: 'Recepción' }} />
          <Stack.Screen name="traspasos/index" options={{ title: 'Traspasos' }} />
          <Stack.Screen name="traspasos/nuevo" options={{ title: 'Nuevo traspaso' }} />
          <Stack.Screen name="traspasos/[id]" options={{ title: 'Traspaso' }} />
          <Stack.Screen name="existencias" options={{ title: 'Existencias' }} />
          <Stack.Screen name="movimientos" options={{ title: 'Movimientos' }} />
          <Stack.Screen name="kardex" options={{ title: 'Kardex' }} />
          <Stack.Screen name="productos/index" options={{ title: 'Productos' }} />
          <Stack.Screen name="productos/editar" options={{ title: 'Producto' }} />
          <Stack.Screen name="productos/[id]" options={{ title: 'Producto' }} />
          <Stack.Screen name="aprobaciones" options={{ title: 'Aprobaciones' }} />
          <Stack.Screen name="almacenes" options={{ title: 'Almacenes' }} />
          <Stack.Screen name="clientes/index" options={{ title: 'Clientes' }} />
          <Stack.Screen name="clientes/editar" options={{ title: 'Cliente' }} />
          <Stack.Screen name="proveedores/index" options={{ title: 'Proveedores' }} />
          <Stack.Screen name="proveedores/editar" options={{ title: 'Proveedor' }} />
          <Stack.Screen name="reportes" options={{ title: 'Reportes' }} />
          <Stack.Screen name="finanzas" options={{ title: 'Movimientos financieros' }} />
          <Stack.Screen name="usuarios" options={{ title: 'Usuarios' }} />
          <Stack.Screen name="visor-pdf" options={{ title: 'PDF' }} />
        </Stack.Protected>
      </Stack>
    </ThemeProvider>
  );
}
