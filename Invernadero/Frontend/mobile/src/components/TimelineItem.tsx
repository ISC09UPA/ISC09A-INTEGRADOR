import { colores } from '@/theme/tokens';
import { MaterialCommunityIcons, MaterialIcons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import type { ComponentProps } from 'react';
import { Text, View } from 'react-native';
import { EventoTimeline } from '../../types';
import { relativo } from '../../utils/formato';

type NombreIcono = ComponentProps<typeof MaterialIcons>['name'];

const ESTILOS: Record<
  EventoTimeline['tipo'],
  { icono: NombreIcono; nodo: string; iconoColor: string; etiqueta: string }
> = {
  riego: { icono: 'water-drop', nodo: 'bg-secondary-container', iconoColor: colores.onSecondaryContainer, etiqueta: 'Riego' },
  alerta: { icono: 'warning', nodo: 'bg-error-container', iconoColor: colores.onErrorContainer, etiqueta: 'Alerta' },
  clima: { icono: 'air', nodo: 'bg-surface-container-high', iconoColor: colores.tertiary, etiqueta: 'Clima' },
  ia: { icono: 'auto-awesome', nodo: 'bg-primary-container', iconoColor: colores.onPrimaryContainer, etiqueta: 'Azure IA' },
};

export function TimelineItem({ evento }: { evento: EventoTimeline }) {
  const est = ESTILOS[evento.tipo];
  return (
    <View className="flex-row items-start gap-3">
      <View className={`h-7 w-7 shrink-0 items-center justify-center rounded-full shadow-sm ${est.nodo}`}>
        <MaterialIcons name={est.icono} size={16} color={est.iconoColor} />
      </View>
      <View className="min-w-0 flex-1 rounded-xl bg-surface-container-lowest p-3.5 shadow-sm">
        <View className="flex-row items-center justify-between">
          <Text
            className={`text-label-sm font-semibold uppercase ${
              evento.tipo === 'alerta' ? 'text-error' : 'text-on-surface-variant'
            }`}
          >
            {relativo(evento.fecha)}
          </Text>
          {evento.plantaNombre && (
            <View
              className={`rounded-full px-2 py-0.5 ${
                evento.tipo === 'alerta' ? 'bg-error-container' : 'bg-surface-container'
              }`}
            >
              <Text
                className={`text-label-sm ${
                  evento.tipo === 'alerta' ? 'text-on-error-container' : 'text-on-surface-variant'
                }`}
                numberOfLines={1}
              >
                {evento.plantaNombre}
              </Text>
            </View>
          )}
        </View>

        <Text className="mt-1 text-title-sm text-on-surface" style={{ fontWeight: '600' }}>
          {evento.titulo}
        </Text>
        <Text className="mt-0.5 text-body-sm text-on-surface-variant">{evento.descripcion}</Text>

        {evento.imagenUrl && (
          <View className="mt-2 h-24 overflow-hidden rounded-lg">
            <Image source={{ uri: evento.imagenUrl }} style={{ width: '100%', height: '100%' }} />
            <View className="absolute bottom-1.5 right-2 flex-row items-center gap-1 rounded-full bg-inverse-surface/80 px-2 py-0.5">
              <MaterialCommunityIcons name="cctv" size={12} color={colores.inversePrimary} />
              <Text className="text-label-sm text-inverse-on-surface">CAM-02 RPi</Text>
            </View>
          </View>
        )}

        {evento.tipo === 'riego' && evento.humedadAntes !== undefined && (
          <View className="mt-2 flex-row items-center justify-between rounded-lg bg-surface-container-low p-2.5">
            <View className="flex-row items-center gap-2">
              <View className="flex-col">
                <Text className="text-label-sm text-on-surface-variant">Humedad inicial</Text>
                <Text className="text-title-sm font-bold text-on-surface">{evento.humedadAntes}%</Text>
              </View>
              <MaterialIcons name="trending-flat" size={18} color={colores.secondary} />
              <View className="flex-col">
                <Text className="text-label-sm text-on-surface-variant">Optimizada</Text>
                <Text className="text-title-sm font-bold text-primary">{evento.humedadDespues}%</Text>
              </View>
            </View>
            <View className="items-end">
              <Text className="text-label-sm text-on-surface-variant">Volumen</Text>
              <Text className="text-title-sm font-bold text-secondary">{evento.volumenMl} mL</Text>
            </View>
          </View>
        )}

        {evento.nivelDepositoPct !== undefined && (
          <View className="mt-2">
            <View className="h-2 w-full overflow-hidden rounded-full bg-surface-container-high">
              <View className="h-full rounded-full bg-tertiary" style={{ width: `${evento.nivelDepositoPct}%` }} />
            </View>
            <Text className="pt-1 text-label-sm text-on-surface-variant">
              Capacidad: {evento.nivelDepositoPct}% del depósito
            </Text>
          </View>
        )}

        {evento.picoTemperaturaC !== undefined && (
          <View className="mt-2 flex-row items-center gap-3 rounded-lg bg-surface-container-low p-2">
            <MaterialIcons name="device-thermostat" size={18} color={colores.tertiary} />
            <Text className="text-label-sm text-on-surface">
              Pico: <Text style={{ fontWeight: '700' }}>{evento.picoTemperaturaC} °C</Text>
            </Text>
          </View>
        )}

        {evento.tipo === 'ia' && evento.certezaVigorPct !== undefined && (
          <View className="mt-2 flex-row gap-2">
            <View className="rounded-full bg-primary-container/40 px-2 py-0.5">
              <Text className="text-label-sm text-primary">
                ✓ {evento.certezaVigorPct}% de vigor
              </Text>
            </View>
          </View>
        )}
      </View>
    </View>
  );
}