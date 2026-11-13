import { useColorScheme } from 'nativewind';
import { Pressable, ScrollView, Switch, Text, View } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';
import { Link, router } from 'expo-router';
import { telemetriaService } from '@/services';
import { colores } from '@/theme/tokens';
import { useDispositivo } from '@/hooks/useTelemetria';
import { useSesion } from '../../store/useSesionStore';
import { Confirmar } from '@/components/Confirmar';
import { useState } from 'react';
import { hapticoExito, hapticoToque } from '../../utils/haptico';
import { relativo } from '../../utils/formato';

const ETIQUETA_ROL: Record<string, string> = {
  admin: 'Administrador · control total',
  operador: 'Operador · lectura y acciones básicas',
};

export default function Perfil() {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;
  const { usuario, cerrarSesion } = useSesion();
  const dispositivo = useDispositivo();
  const [cerrar, setCerrar] = useState(false);

  const online = dispositivo.data?.online ?? false;

  function alternarOnline(v: boolean) {
    hapticoToque();
    void telemetriaService.establecerDispositivoEnLinea(v).then(() => {
      void dispositivo.refetch();
      hapticoExito();
    });
  }

  return (
    <View className="flex-1 bg-surface">
      <View className="h-16 flex-row items-center px-gutter-mobile">
        <Pressable onPress={() => router.back()} accessibilityLabel="Volver">
          <MaterialIcons name="arrow-back" size={22} color={tema.onSurface} />
        </Pressable>
      </View>

      <ScrollView contentContainerStyle={{ paddingBottom: 24 }} showsVerticalScrollIndicator={false}>
        {/* Tarjeta usuario grande */}
        <View className="items-center pb-4">
          <View>
            <View className="h-20 w-20 items-center justify-center rounded-full bg-primary">
              <Text className="text-2xl font-bold text-on-primary">
                {(usuario?.nombre ?? 'US').slice(0, 2).toUpperCase()}
              </Text>
            </View>
            <View
              className="absolute -bottom-1 -right-1 h-5 w-5 items-center justify-center rounded-full bg-surface-container-lowest"
              accessibilityLabel="Estatus en línea"
            >
              <View className={`h-3 w-3 rounded-full ${online ? 'bg-primary' : 'bg-error'}`} />
            </View>
          </View>
          <Text className="pt-3 text-headline-lg font-bold text-on-surface">{usuario?.nombre ?? 'Usuario'}</Text>
          <Text className="text-body-md text-on-surface-variant">{usuario?.email ?? ''}</Text>
          <View className="mt-1 rounded-full bg-primary-container px-3 py-1">
            <Text className="text-label-md font-semibold text-on-primary-container">
              {ETIQUETA_ROL[usuario?.rol ?? 'operador']}
            </Text>
          </View>
        </View>

        {/* Modo dispositivo */}
        <View className="px-gutter-mobile pb-2">
          <Text className="text-label-lg pb-2 text-on-surface-variant">Ajustes de la demo</Text>
          <View className="flex-row items-center justify-between rounded-xl bg-surface-container-lowest p-3 shadow-sm">
            <View className="flex-1 pr-2">
              <Text className="text-title-sm text-on-surface">Modo RPi: {online ? 'Online' : 'Offline'}</Text>
              <Text className="text-body-sm text-on-surface-variant">
                Alterna para probar estados offline. Los controles manuales se deshabilitan y el banner muestra la hora
                de la última sincronización ({dispositivo.data ? relativo(dispositivo.data.ultimaSync) : '—'}).
              </Text>
            </View>
            <Switch value={online} onValueChange={alternarOnline} trackColor={{ false: tema.surfaceContainerHighest, true: tema.primary }} accessibilityLabel="Alternar RPi Online" />
          </View>
          <Link href="/notificaciones" asChild>
            <Pressable
              className="mt-2 flex-row items-center justify-between rounded-xl bg-surface-container-lowest p-3 shadow-sm active:scale-[0.98]"
              accessibilityRole="button"
              accessibilityLabel="Abrir notificaciones"
            >
              <View className="flex-row items-center gap-2">
                <View className="h-9 w-9 items-center justify-center rounded-xl bg-secondary-container">
                  <MaterialIcons name="notifications" size={20} color={tema.onSecondaryContainer} />
                </View>
                <Text className="text-title-sm text-on-surface">Notificaciones y permisos</Text>
              </View>
              <MaterialIcons name="chevron-right" size={22} color={tema.onSurfaceVariant} />
            </Pressable>
          </Link>
        </View>

        {/* Info de versión */}
        <View className="px-gutter-mobile pb-2">
          <View className="rounded-xl bg-surface-container-low p-3">
            <View className="flex-row items-center gap-1.5">
              <MaterialIcons name="eco" size={18} color={tema.primary} />
              <Text className="text-title-sm font-semibold text-on-surface">RiegoSmart IA</Text>
            </View>
            <Text className="pt-1 text-body-sm text-on-surface-variant">Versión demo 1.0 · Expo SDK 58</Text>
            <Text className="text-[11px] text-on-surface-variant">
              Datos simulados en la capa services/, lista para sustituir por la API real.
            </Text>
          </View>
        </View>

        {/* Cerrar sesión */}
        <View className="px-gutter-mobile pt-2">
          <Pressable
            onPress={() => {
              hapticoToque();
              setCerrar(true);
            }}
            className="min-h-[48px] items-center justify-center rounded-xl bg-error-container py-3 active:scale-[0.98]"
            accessibilityRole="button"
            accessibilityLabel="Cerrar sesión"
          >
            <View className="flex-row items-center gap-2">
              <MaterialIcons name="logout" size={18} color={tema.onErrorContainer} />
              <Text className="text-label-md font-semibold text-on-error-container">Cerrar sesión</Text>
            </View>
          </Pressable>
        </View>
      </ScrollView>

      <Confirmar
        visible={cerrar}
        titulo="Cerrar sesión"
        descripcion="¿Estás seguro? Volverás a la pantalla de login."
        textoConfirmar="Cerrar sesión"
        peligroso
        onCancelar={() => setCerrar(false)}
        onConfirmar={() => {
          setCerrar(false);
          void cerrarSesion().then(() => router.replace('/(auth)/login'));
        }}
      />
    </View>
  );
}
