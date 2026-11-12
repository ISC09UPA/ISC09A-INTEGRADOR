import { MaterialCommunityIcons, MaterialIcons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import type { ComponentProps } from 'react';
import { Text, View } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';
import { EventoTimeline } from '../../types';
import { relativo } from '../../utils/formato';

type NombreIcono = ComponentProps<typeof MaterialIcons>['name'];

export function TimelineItem({
  evento,
  isLast = false,
}: {
  evento: EventoTimeline;
  isLast?: boolean;
}) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const ESTILOS: Record<
    EventoTimeline['tipo'],
    { icono: NombreIcono; fondo: string; iconoColor: string; etiqueta: string }
  > = {
    riego: {
      icono: 'water-drop',
      fondo: tema.secondaryContainer,
      iconoColor: tema.onSecondaryContainer,
      etiqueta: 'Riego',
    },
    alerta: {
      icono: 'warning',
      fondo: tema.errorContainer,
      iconoColor: tema.onErrorContainer,
      etiqueta: 'Alerta',
    },
    clima: {
      icono: 'air',
      fondo: tema.surfaceContainerHigh,
      iconoColor: tema.tertiary,
      etiqueta: 'Clima',
    },
    ia: {
      icono: 'auto-awesome',
      fondo: tema.primaryContainer,
      iconoColor: tema.onPrimaryContainer,
      etiqueta: 'Azure IA',
    },
  };

  const est = ESTILOS[evento.tipo];

  return (
    <View className="relative">
      {/* Línea conectora: termina en el siguiente indicador */}
      {!isLast && (
        <View
          className="absolute bg-surface-container-high"
          pointerEvents="none"
          style={{
            left: 13,
            top: 14,
            bottom: -26,
            width: 2,
            zIndex: 0,
          }}
        />
      )}

      <View className="flex-row items-start gap-3">
        {/* Indicador del evento */}
        <View
          className="z-10 h-7 w-7 shrink-0 items-center justify-center rounded-full shadow-sm"
          style={{ backgroundColor: est.fondo }}
        >
          <MaterialIcons name={est.icono} size={16} color={est.iconoColor} />
        </View>

        {/* Contenido */}
        <View
          className="min-w-0 flex-1 rounded-xl p-3.5 shadow-sm"
          style={{ backgroundColor: tema.surfaceContainerLowest }}
        >
          <View className="flex-row items-center justify-between">
            <Text
              className="text-label-sm font-semibold uppercase"
              style={{
                color: evento.tipo === 'alerta' ? tema.error : tema.onSurfaceVariant,
              }}
            >
              {relativo(evento.fecha)}
            </Text>

            {evento.plantaNombre && (
              <View
                className="rounded-full px-2 py-0.5"
                style={{
                  backgroundColor:
                    evento.tipo === 'alerta'
                      ? tema.errorContainer
                      : tema.surfaceContainer,
                }}
              >
                <Text
                  className="text-label-sm"
                  style={{
                    color:
                      evento.tipo === 'alerta'
                        ? tema.onErrorContainer
                        : tema.onSurfaceVariant,
                  }}
                  numberOfLines={1}
                >
                  {evento.plantaNombre}
                </Text>
              </View>
            )}
          </View>

          <Text
            className="mt-1 text-title-sm"
            style={{ fontWeight: '600', color: tema.onSurface }}
          >
            {evento.titulo}
          </Text>

          <Text
            className="mt-0.5 text-body-sm"
            style={{ color: tema.onSurfaceVariant }}
          >
            {evento.descripcion}
          </Text>

          {evento.imagenUrl && (
            <View className="mt-2 h-24 overflow-hidden rounded-lg">
              <Image
                source={{ uri: evento.imagenUrl }}
                style={{ width: '100%', height: '100%' }}
              />

              <View className="absolute bottom-1.5 right-2 flex-row items-center gap-1 rounded-full bg-black/70 px-2 py-0.5">
                <MaterialCommunityIcons
                  name="cctv"
                  size={12}
                  color={tema.inversePrimary}
                />
                <Text className="text-label-sm text-white">CAM-02 RPi</Text>
              </View>
            </View>
          )}

          {evento.tipo === 'riego' && evento.humedadAntes !== undefined && (
            <View
              className="mt-2 flex-row items-center justify-between rounded-lg p-2.5"
              style={{ backgroundColor: tema.surfaceContainerLow }}
            >
              <View className="flex-row items-center gap-2">
                <View className="flex-col">
                  <Text className="text-label-sm" style={{ color: tema.onSurfaceVariant }}>
                    Humedad inicial
                  </Text>
                  <Text className="text-title-sm font-bold" style={{ color: tema.onSurface }}>
                    {evento.humedadAntes}%
                  </Text>
                </View>

                <MaterialIcons name="trending-flat" size={18} color={tema.secondary} />

                <View className="flex-col">
                  <Text className="text-label-sm" style={{ color: tema.onSurfaceVariant }}>
                    Optimizada
                  </Text>
                  <Text className="text-title-sm font-bold" style={{ color: tema.primary }}>
                    {evento.humedadDespues}%
                  </Text>
                </View>
              </View>

              <View className="items-end">
                <Text className="text-label-sm" style={{ color: tema.onSurfaceVariant }}>
                  Volumen
                </Text>
                <Text className="text-title-sm font-bold" style={{ color: tema.secondary }}>
                  {evento.volumenMl} mL
                </Text>
              </View>
            </View>
          )}

          {evento.nivelDepositoPct !== undefined && (
            <View className="mt-2">
              <View
                className="h-2 w-full overflow-hidden rounded-full"
                style={{ backgroundColor: tema.surfaceContainerHigh }}
              >
                <View
                  className="h-full rounded-full"
                  style={{
                    width: `${evento.nivelDepositoPct}%`,
                    backgroundColor: tema.tertiary,
                  }}
                />
              </View>

              <Text className="pt-1 text-label-sm" style={{ color: tema.onSurfaceVariant }}>
                Capacidad: {evento.nivelDepositoPct}% del depósito
              </Text>
            </View>
          )}

          {evento.picoTemperaturaC !== undefined && (
            <View
              className="mt-2 flex-row items-center gap-3 rounded-lg p-2"
              style={{ backgroundColor: tema.surfaceContainerLow }}
            >
              <MaterialIcons name="device-thermostat" size={18} color={tema.tertiary} />
              <Text className="text-label-sm" style={{ color: tema.onSurface }}>
                Pico: <Text style={{ fontWeight: '700' }}>{evento.picoTemperaturaC} °C</Text>
              </Text>
            </View>
          )}

          {evento.tipo === 'ia' && evento.certezaVigorPct !== undefined && (
            <View className="mt-2 flex-row gap-2">
              <View
                className="rounded-full px-2 py-0.5"
                style={{ backgroundColor: `${tema.primaryContainer}66` }}
              >
                <Text className="text-label-sm" style={{ color: tema.primary }}>
                  ✓ {evento.certezaVigorPct}% de vigor
                </Text>
              </View>
            </View>
          )}
        </View>
      </View>
    </View>
  );
}