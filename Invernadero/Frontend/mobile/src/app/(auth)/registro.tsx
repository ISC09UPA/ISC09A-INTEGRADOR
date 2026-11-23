import { useColorScheme } from 'nativewind';
import { Boton } from '@/components/ui/Botones';
import { useSesion } from '../../../store/useSesionStore';
import { colores } from '@/theme/tokens';
import { MaterialIcons } from '@expo/vector-icons';
import * as ExpoHaptics from 'expo-haptics';
import { Link, Redirect } from 'expo-router';
import { useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  Text,
  TextInput,
  type TextInputProps,
  View,
} from 'react-native';

const EMAIL_REGEXP = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

type Errores = {
  nombre?: string;
  correo?: string;
  contrasena?: string;
  confirmar?: string;
  general?: string;
};

interface CampoProps extends TextInputProps {
  etiqueta: string;
  error?: string;
  colorPlaceholder: string;
}

/** Campo de texto con etiqueta y mensaje de error (mismo estilo que el login). */
function Campo({ etiqueta, error, colorPlaceholder, ...props }: CampoProps) {
  return (
    <View>
      <Text className="mb-1 text-label-lg text-on-surface-variant">{etiqueta}</Text>
      <TextInput
        accessibilityLabel={etiqueta}
        placeholderTextColor={colorPlaceholder}
        className={`rounded-xl bg-surface-container-lowest px-4 py-3 text-body-md text-on-surface min-h-[48px] ${error ? 'border border-error' : ''}`}
        {...props}
      />
      {error && <Text className="text-label-md text-error">{error}</Text>}
    </View>
  );
}

export default function Registro() {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;
  const { usuario, registrar } = useSesion();

  const [nombre, setNombre] = useState('');
  const [correo, setCorreo] = useState('');
  const [contrasena, setContrasena] = useState('');
  const [confirmar, setConfirmar] = useState('');
  const [errores, setErrores] = useState<Errores>({});
  const [cargando, setCargando] = useState(false);

  if (usuario) return <Redirect href="/(tabs)/inicio" />;

  async function crearCuenta() {
    const nuevos: Errores = {};
    if (nombre.trim().length < 2) nuevos.nombre = 'Escribe tu nombre.';
    if (!EMAIL_REGEXP.test(correo.trim())) nuevos.correo = 'Correo inválido.';
    if (contrasena.length < 6) nuevos.contrasena = 'Mínimo 6 caracteres.';
    if (confirmar !== contrasena) nuevos.confirmar = 'Las contraseñas no coinciden.';
    setErrores(nuevos);
    if (Object.keys(nuevos).length > 0) return;

    setCargando(true);
    try {
      await registrar(nombre, correo, contrasena);
      // La sesión en el store dispara el <Redirect> hacia (tabs).
      ExpoHaptics.notificationAsync(ExpoHaptics.NotificationFeedbackType.Success).catch(() => undefined);
    } catch (e) {
      ExpoHaptics.notificationAsync(ExpoHaptics.NotificationFeedbackType.Error).catch(() => undefined);
      setErrores({ general: e instanceof Error ? e.message : 'No se pudo crear la cuenta.' });
    } finally {
      setCargando(false);
    }
  }

  return (
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} className="flex-1 bg-surface">
      <ScrollView
        contentContainerStyle={{ flexGrow: 1, justifyContent: 'center' }}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
      >
        <View className="px-gutter-mobile py-8">
          <View className="mb-8 items-center gap-2">
            <View className="h-16 w-16 items-center justify-center rounded-2xl bg-primary-container">
              <MaterialIcons name="person-add" size={32} color={tema.primary} />
            </View>
            <Text className="text-headline-lg-mobile text-on-surface" style={{ fontWeight: '700' }}>
              Crear cuenta
            </Text>
            <Text className="text-center text-body-md text-on-surface-variant">
              Las cuentas nuevas entran como operador. Un administrador puede cambiar tu rol.
            </Text>
          </View>

          <View className="gap-3">
            <Campo
              etiqueta="Nombre completo"
              value={nombre}
              onChangeText={setNombre}
              placeholder="Tu nombre"
              autoComplete="name"
              error={errores.nombre}
              colorPlaceholder={tema.onSurfaceVariant}
            />
            <Campo
              etiqueta="Correo electrónico"
              value={correo}
              onChangeText={setCorreo}
              placeholder="correo@ejemplo.com"
              autoCapitalize="none"
              keyboardType="email-address"
              autoComplete="email"
              error={errores.correo}
              colorPlaceholder={tema.onSurfaceVariant}
            />
            <Campo
              etiqueta="Contraseña"
              value={contrasena}
              onChangeText={setContrasena}
              placeholder="Mínimo 6 caracteres"
              secureTextEntry
              error={errores.contrasena}
              colorPlaceholder={tema.onSurfaceVariant}
            />
            <Campo
              etiqueta="Confirmar contraseña"
              value={confirmar}
              onChangeText={setConfirmar}
              placeholder="Repite la contraseña"
              secureTextEntry
              error={errores.confirmar}
              colorPlaceholder={tema.onSurfaceVariant}
            />

            {errores.general && (
              <View className="rounded-xl bg-error-container px-3 py-2.5">
                <Text className="text-body-sm text-on-error-container">{errores.general}</Text>
              </View>
            )}

            <Boton texto="Registrarme" onPress={() => void crearCuenta()} cargando={cargando} className="mt-2" />

            <Link href="/(auth)/login" asChild>
              <Pressable className="min-h-[44px] items-center justify-center" accessibilityRole="button">
                <Text className="text-label-lg text-primary" style={{ fontWeight: '600' }}>
                  ¿Ya tienes cuenta? Inicia sesión
                </Text>
              </Pressable>
            </Link>
          </View>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}
