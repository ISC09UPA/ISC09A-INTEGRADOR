import { MaterialIcons } from '@expo/vector-icons';
import { Pressable, Text, View, type DimensionValue } from 'react-native';
import { colores } from '@/theme/tokens';

export function Skeleton({
  ancho = '100%' as DimensionValue,
  alto = 16,
  className = '',
}: {
  ancho?: DimensionValue;
  alto?: number;
  className?: string;
}) {
  return (
    <View
      className={`rounded-lg bg-surface-container-high ${className}`}
      style={{ width: ancho, height: alto }}
    />
  );
}

export function Vacio({
  titulo,
  descripcion,
  accionTexto,
  accionOn,
}: {
  titulo: string;
  descripcion: string;
  accionTexto?: string;
  accionOn?: () => void;
}) {
  return (
    <View className="items-center justify-center gap-2 rounded-xl bg-surface-container-lowest p-6 shadow-sm">
      <View className="h-12 w-12 items-center justify-center rounded-full bg-surface-container-high">
        <MaterialIcons name="filter-list-off" size={24} color={colores.onSurfaceVariant} />
      </View>
      <Text className="text-title-sm text-on-surface" style={{ fontWeight: '600' }}>
        {titulo}
      </Text>
      <Text className="max-w-[240px] text-center text-body-sm text-on-surface-variant">{descripcion}</Text>
      {accionTexto && accionOn && (
        <Pressable onPress={accionOn} className="mt-2 rounded-lg px-3 py-2 min-h-[44px] justify-center">
          <Text className="text-title-sm text-primary" style={{ fontWeight: '600' }}>
            {accionTexto}
          </Text>
        </Pressable>
      )}
    </View>
  );
}

export function ErrorConReintento({
  mensaje,
  intentarDeNuevo,
}: {
  mensaje: string;
  intentarDeNuevo: () => void;
}) {
  return (
    <View className="items-center gap-3 rounded-xl bg-error-container p-5">
      <MaterialIcons name="error-outline" size={26} color={colores.onErrorContainer} />
      <Text className="text-body-md text-on-error-container text-center">{mensaje}</Text>
      <Pressable
        onPress={intentarDeNuevo}
        accessibilityRole="button"
        className="rounded-xl bg-error px-4 py-2 min-h-[44px] justify-center"
      >
        <Text className="text-label-lg text-on-error" style={{ fontWeight: '600' }}>
          Reintentar
        </Text>
      </Pressable>
    </View>
  );
}

export function BannerOffline({ ultimaSync }: { ultimaSync: string }) {
  const hora = new Date(ultimaSync).toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' });
  return (
    <View className="flex-row items-center gap-2 rounded-xl bg-tertiary-container px-3 py-2.5">
      <MaterialIcons name="cloud-off" size={18} color={colores.onTertiaryContainer} />
      <View className="flex-1">
        <Text className="text-label-md text-on-tertiary-container" style={{ fontWeight: '600' }}>
          Sin conexión con la RPi · Mostrando últimos datos ({hora} h)
        </Text>
        <Text className="text-[11px] text-on-tertiary-container">
          Los controles manuales están deshabilitados hasta que la Raspberry Pi vuelva a estar en línea.
        </Text>
      </View>
    </View>
  );
}
