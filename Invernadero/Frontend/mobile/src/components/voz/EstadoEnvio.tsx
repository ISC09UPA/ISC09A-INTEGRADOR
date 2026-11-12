import { ActivityIndicator, Text, View } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';

import { Boton } from '@/components/ui/Botones';
import { Card } from '@/components/ui/Card';
import { Chip } from '@/components/ui/Chip';
import type { Grabacion } from '@/voz/tipos';
import { formatearDuracion } from '@/voz/formato';
import { Transcripcion } from './Transcripcion';
import { useTema } from './useTema';

interface Props {
  fase: 'sent' | 'processing';
  grabacion: Grabacion | null;
  /** El envío sigue siendo simulado (sin backend). Se indica en pantalla. */
  envioSimulado: boolean;
  transcripcionSimulada: boolean;
  /**
   * Respuesta del asistente. Reservado para cuando el backend la entregue;
   * mientras sea null solo se muestra el estado de espera.
   */
  respuesta?: string | null;
  onNueva: () => void;
}

export function EstadoEnvio({
  fase,
  grabacion,
  envioSimulado,
  transcripcionSimulada,
  respuesta = null,
  onNueva,
}: Props) {
  const tema = useTema();

  if (fase === 'sent') {
    return (
      <View style={{ gap: 16 }}>
        <View style={{ alignItems: 'center', gap: 10 }}>
          <View
            style={{
              width: 84,
              height: 84,
              borderRadius: 42,
              alignItems: 'center',
              justifyContent: 'center',
              backgroundColor: tema.primaryContainer,
            }}
          >
            <MaterialIcons name="check-circle" size={52} color={tema.primary} />
          </View>
          <Text style={{ fontSize: 22, fontWeight: '600', textAlign: 'center', color: tema.onSurface }}>
            Audio enviado correctamente
          </Text>
          <Chip variante="exito" texto="Enviado" />
          {envioSimulado ? (
            <Text style={{ fontSize: 12, textAlign: 'center', color: tema.onSurfaceVariant }}>
              Envío simulado: aún no hay conexión con el backend.
            </Text>
          ) : null}
        </View>

        <Card tono="bajo">
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
            <MaterialIcons name="schedule" size={18} color={tema.onSurfaceVariant} />
            <Text style={{ fontSize: 14, color: tema.onSurfaceVariant }}>Duración</Text>
            <Text style={{ fontSize: 14, fontWeight: '600', color: tema.onSurface }}>
              {formatearDuracion(grabacion?.duracionMs ?? 0)}
            </Text>
          </View>
        </Card>

        <Transcripcion texto={grabacion?.transcripcion ?? ''} simulada={transcripcionSimulada} />
      </View>
    );
  }

  return (
    <View style={{ gap: 16 }}>
      <View style={{ alignItems: 'center', gap: 12, paddingVertical: 8 }}>
        <ActivityIndicator size="large" color={tema.primary} />
        <Text style={{ fontSize: 22, fontWeight: '600', textAlign: 'center', color: tema.onSurface }}>
          Procesando solicitud
        </Text>
        <Text style={{ fontSize: 15, textAlign: 'center', color: tema.onSurfaceVariant }}>
          Esperando la respuesta del asistente...
        </Text>
      </View>

      {/* Sección preparada para la respuesta del backend. */}
      {respuesta ? (
        <Card tono="bajo" style={{ gap: 6 }}>
          <Text style={{ fontSize: 12, fontWeight: '600', letterSpacing: 0.5, color: tema.onSurfaceVariant }}>
            RESPUESTA DEL ASISTENTE
          </Text>
          <Text style={{ fontSize: 16, lineHeight: 24, color: tema.onSurface }}>{respuesta}</Text>
        </Card>
      ) : null}

      <Transcripcion texto={grabacion?.transcripcion ?? ''} simulada={transcripcionSimulada} alturaMaxima={120} />

      {envioSimulado ? (
        <Text style={{ fontSize: 12, textAlign: 'center', color: tema.onSurfaceVariant }}>
          Estado simulado: no hay solicitud real en curso mientras no exista el backend.
        </Text>
      ) : null}

      <Boton variante="secundario" texto="Nueva grabación" onPress={onNueva} accessibilityRole="button" />
    </View>
  );
}
