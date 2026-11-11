import { Boton } from '@/components/ui/Botones';
import { useSesion } from '../../../store/useSesionStore';
import { colores } from '@/theme/tokens';
import { MaterialIcons } from '@expo/vector-icons';
import * as ExpoHaptics from 'expo-haptics';
import { Redirect } from 'expo-router';
import { useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, Text, TextInput, View } from 'react-native';

const EMAIL_REGEXP = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export default function Login() {
  const { usuario, iniciarSesion, cerrarSesion } = useSesion();

  const [correo, setCorreo] = useState('');
  const [contrasena, setContrasena] = useState('');
  const [errores, setErrores] = useState<{ correo?: string; contrasena?: string; general?: string }>({});
  const [cargando, setCargando] = useState(false);

  if (usuario) return <Redirect href="/(tabs)/inicio" />;

  async function entrar(email = correo, pass = contrasena) {
    setErrores({});
    if (!EMAIL_REGEXP.test(email.trim()) || pass.length < 1) {
      setErrores({ general: 'Correo o contraseña incompletos.' });
      return;
    }
    setCargando(true);
    try {
      await iniciarSesion(email.trim(), pass);
      // La sesión en el store dispara el <Redirect> hacia (tabs).
      ExpoHaptics.notificationAsync(ExpoHaptics.NotificationFeedbackType.Success).catch(() => undefined);
    } catch (e) {
      ExpoHaptics.notificationAsync(ExpoHaptics.NotificationFeedbackType.Error).catch(() => undefined);
      setErrores({ general: e instanceof Error ? e.message : 'No se pudo iniciar sesión.' });
    } finally {
      setCargando(false);
    }
  }

  const rellenarDemo = async (demoCorreo: string) => {
    setCorreo(demoCorreo);
    setContrasena('demo123');
    setErrores({});
  };

  return (
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} className="flex-1 bg-surface">
      <View className="flex-1 justify-center px-gutter-mobile">
        <View className="mb-8 items-center gap-2">
          <View className="h-16 w-16 items-center justify-center rounded-2xl bg-primary-container">
            <MaterialIcons name="eco" size={34} color={colores.primary} />
          </View>
          <Text className="text-headline-lg-mobile text-on-surface" style={{ fontWeight: '700' }}>
            RiegoSmart IA
          </Text>
          <Text className="text-body-md text-on-surface-variant">Invernadero Inteligente</Text>
        </View>

        <View className="gap-3">
          <View>
            <Text className="mb-1 text-label-lg text-on-surface-variant">Correo electrónico</Text>
            <TextInput
              value={correo}
              onChangeText={(t) => setCorreo(t)}
              placeholder="admin@demo.com"
              autoCapitalize="none"
              keyboardType="email-address"
              autoComplete="email"
              accessibilityLabel="Correo electrónico"
              className={`rounded-xl bg-surface-container-lowest px-4 py-3 text-body-md text-on-surface min-h-[48px] ${errores.correo ? 'border border-error' : ''}`}
              placeholderTextColor={colores.onSurfaceVariant}
            />
            {errores.correo && <Text className="text-label-md text-error">{errores.correo}</Text>}
          </View>

          <View>
            <Text className="mb-1 text-label-lg text-on-surface-variant">Contraseña</Text>
            <TextInput
              value={contrasena}
              onChangeText={setContrasena}
              placeholder="••••••••"
              secureTextEntry
              accessibilityLabel="Contraseña"
              className={`rounded-xl bg-surface-container-lowest px-4 py-3 text-body-md text-on-surface min-h-[48px] ${errores.contrasena ? 'border border-error' : ''}`}
              placeholderTextColor={colores.onSurfaceVariant}
            />
            {errores.contrasena && <Text className="text-label-md text-error">{errores.contrasena}</Text>}
          </View>

          {errores.general && (
            <View className="rounded-xl bg-error-container px-3 py-2.5">
              <Text className="text-body-sm text-on-error-container">{errores.general}</Text>
            </View>
          )}

          <Boton texto="Iniciar sesión" onPress={() => void entrar()} cargando={cargando} className="mt-2" />
        </View>

        <View className="mt-6 gap-3">
          <Text className="text-center text-label-md text-on-surface-variant">Usuarios demo (contraseña: demo123)</Text>
          <View className="flex-row gap-2">
            <Pressable
              onPress={() => void rellenarDemo('admin@demo.com')}
              className="flex-1 items-center rounded-xl bg-surface-container-low py-2.5 min-h-[44px] justify-center"
              accessibilityRole="button"
            >
              <Text className="text-label-lg text-on-surface" style={{ fontWeight: '600' }}>
                Admin
              </Text>
              <Text className="text-label-sm text-on-surface-variant">admin@demo.com</Text>
            </Pressable>
            <Pressable
              onPress={() => void rellenarDemo('operador@demo.com')}
              className="flex-1 items-center rounded-xl bg-surface-container-low py-2.5 min-h-[44px] justify-center"
              accessibilityRole="button"
            >
              <Text className="text-label-lg text-on-surface" style={{ fontWeight: '600' }}>
                Operador
              </Text>
              <Text className="text-label-sm text-on-surface-variant">operador@demo.com</Text>
            </Pressable>
          </View>
        </View>

        {/* Kill switch rápido para la demo: cierra sesión activa si quedara alguna. */}
        <Pressable
          onPress={() => void cerrarSesion()}
          className="mt-10 items-center py-2"
          accessibilityRole="button"
          accessibilityLabel="Limpiar sesión guardada"
        >
          <Text className="text-label-md text-on-surface-variant" style={{ fontWeight: '500' }}>
            ¿Problemas con tu sesión? Toca aquí para limpiarla
          </Text>
        </Pressable>
      </View>
    </KeyboardAvoidingView>
  );
}

