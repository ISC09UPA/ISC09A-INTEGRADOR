import { MaterialIcons } from '@expo/vector-icons';
import { ActivityIndicator, Pressable, Text, View, type DimensionValue } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

function useTema() {
  const { colorScheme } = useColorScheme();
  return colorScheme === 'dark' ? colores.oscuro : colores.claro;
}

export function Skeleton({
  ancho = '100%',
  alto = 16,
  className = '',
}: {
  ancho?: DimensionValue;
  alto?: number;
  className?: string;
}) {
  const tema = useTema();

  return (
    <View
      className={`rounded-lg ${className}`}
      style={{
        width: ancho,
        height: alto,
        backgroundColor: tema.surfaceContainerHigh,
      }}
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
  const tema = useTema();

  return (
    <View
      className="items-center justify-center gap-2 rounded-xl p-6 shadow-sm"
      style={{ backgroundColor: tema.surfaceContainerLowest }}
    >
      <View
        className="h-12 w-12 items-center justify-center rounded-full"
        style={{ backgroundColor: tema.surfaceContainerHigh }}
      >
        <MaterialIcons
          name="filter-list-off"
          size={24}
          color={tema.onSurfaceVariant}
        />
      </View>

      <Text
        className="text-title-sm"
        style={{ fontWeight: '600', color: tema.onSurface }}
      >
        {titulo}
      </Text>

      <Text
        className="max-w-[240px] text-center text-body-sm"
        style={{ color: tema.onSurfaceVariant }}
      >
        {descripcion}
      </Text>

      {accionTexto && accionOn && (
        <Pressable
          onPress={accionOn}
          className="mt-2 rounded-lg px-3 py-2 min-h-[44px] justify-center"
        >
          <Text
            className="text-title-sm"
            style={{ fontWeight: '600', color: tema.primary }}
          >
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
  const tema = useTema();

  return (
    <View
      className="items-center gap-3 rounded-xl p-5"
      style={{ backgroundColor: tema.errorContainer }}
    >
      <MaterialIcons
        name="error-outline"
        size={26}
        color={tema.onErrorContainer}
      />

      <Text
        className="text-body-md text-center"
        style={{ color: tema.onErrorContainer }}
      >
        {mensaje}
      </Text>

      <Pressable
        onPress={intentarDeNuevo}
        accessibilityRole="button"
        className="rounded-xl px-4 py-2 min-h-[44px] justify-center"
        style={{ backgroundColor: tema.error }}
      >
        <Text
          className="text-label-lg"
          style={{ fontWeight: '600', color: tema.onError }}
        >
          Reintentar
        </Text>
      </Pressable>
    </View>
  );
}

export function BannerOffline({ ultimaSync }: { ultimaSync: string }) {
  const tema = useTema();

  const hora = new Date(ultimaSync).toLocaleTimeString('es-MX', {
    hour: '2-digit',
    minute: '2-digit',
  });

  return (
    <View
      className="flex-row items-center gap-2 rounded-xl px-3 py-2.5"
      style={{ backgroundColor: tema.tertiaryContainer }}
    >
      <MaterialIcons
        name="cloud-off"
        size={18}
        color={tema.onTertiaryContainer}
      />

      <View className="flex-1">
        <Text
          className="text-label-md"
          style={{ fontWeight: '600', color: tema.onTertiaryContainer }}
        >
          Sin conexión con la RPi · Mostrando últimos datos ({hora} h)
        </Text>

        <Text
          className="text-[11px]"
          style={{ color: tema.onTertiaryContainer }}
        >
          Los controles manuales están deshabilitados hasta que la Raspberry Pi vuelva a estar en línea.
        </Text>
      </View>
    </View>
  );
}