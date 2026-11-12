import { ActivityIndicator, Pressable, Text, View } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';
import { useAudioPlayer, useAudioPlayerStatus } from 'expo-audio';

import { Card } from '@/components/ui/Card';
import type { Grabacion } from '@/voz/tipos';
import { formatearDuracion } from '@/voz/formato';
import { Transcripcion } from './Transcripcion';
import { useTema } from './useTema';

interface Props {
  grabacion: Grabacion;
  enviando: boolean;
  transcripcionSimulada: boolean;
  onConfirmar: () => void;
  onEliminar: () => void;
}

/** Reproductor opcional para escuchar el audio antes de confirmarlo. */
function ReproductorAudio({ uri, duracionMs }: { uri: string; duracionMs: number }) {
  const tema = useTema();
  const player = useAudioPlayer(uri, { updateInterval: 200 });
  const estado = useAudioPlayerStatus(player);

  const total = estado.duration > 0 ? estado.duration : duracionMs / 1000;
  const progreso = total > 0 ? Math.min(1, estado.currentTime / total) : 0;

  const alternar = () => {
    if (estado.playing) {
      player.pause();
      return;
    }
    if (estado.didJustFinish || estado.currentTime >= total - 0.05) {
      void player.seekTo(0);
    }
    player.play();
  };

  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: 12 }}>
      <Pressable
        onPress={alternar}
        accessibilityRole="button"
        accessibilityLabel={estado.playing ? 'Pausar audio' : 'Escuchar audio'}
        style={{
          width: 44,
          height: 44,
          borderRadius: 22,
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: tema.primaryContainer,
        }}
      >
        <MaterialIcons
          name={estado.playing ? 'pause' : 'play-arrow'}
          size={26}
          color={tema.onPrimaryContainer}
        />
      </Pressable>

      <View style={{ flex: 1, gap: 4 }}>
        <View style={{ height: 6, borderRadius: 3, backgroundColor: tema.surfaceContainerHighest, overflow: 'hidden' }}>
          <View style={{ height: 6, width: `${progreso * 100}%`, backgroundColor: tema.primary }} />
        </View>
        <Text style={{ fontSize: 12, color: tema.onSurfaceVariant }}>
          {formatearDuracion(estado.currentTime * 1000)} / {formatearDuracion(total * 1000)}
        </Text>
      </View>
    </View>
  );
}

export function RevisionGrabacion({
  grabacion,
  enviando,
  transcripcionSimulada,
  onConfirmar,
  onEliminar,
}: Props) {
  const tema = useTema();

  return (
    <View style={{ gap: 16 }}>
      <View style={{ alignItems: 'center', gap: 8 }}>
        <View
          style={{
            flexDirection: 'row',
            alignItems: 'center',
            gap: 6,
            paddingHorizontal: 12,
            paddingVertical: 5,
            borderRadius: 999,
            backgroundColor: tema.primaryContainer,
          }}
        >
          <MaterialIcons name="check-circle" size={16} color={tema.onPrimaryContainer} />
          <Text style={{ fontSize: 12, fontWeight: '600', color: tema.onPrimaryContainer }}>
            Grabación terminada
          </Text>
        </View>

        <Text style={{ fontSize: 22, fontWeight: '600', textAlign: 'center', color: tema.onSurface }}>
          ¿Quieres conservar esta grabación?
        </Text>
      </View>

      <Card tono="bajo" style={{ gap: 12 }}>
        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
          <MaterialIcons name="schedule" size={18} color={tema.onSurfaceVariant} />
          <Text style={{ fontSize: 14, color: tema.onSurfaceVariant }}>Duración</Text>
          <Text style={{ fontSize: 14, fontWeight: '600', color: tema.onSurface }}>
            {formatearDuracion(grabacion.duracionMs)}
          </Text>
        </View>
        {grabacion.uri ? (
          <ReproductorAudio uri={grabacion.uri} duracionMs={grabacion.duracionMs} />
        ) : null}
      </Card>

      <Transcripcion texto={grabacion.transcripcion} simulada={transcripcionSimulada} />

      <View style={{ gap: 10 }}>
        <Pressable
          onPress={onConfirmar}
          disabled={enviando}
          accessibilityRole="button"
          style={{
            minHeight: 48,
            alignItems: 'center',
            justifyContent: 'center',
            borderRadius: 12,
            padding: 12,
            backgroundColor: tema.primary,
            opacity: enviando ? 0.7 : 1,
          }}
        >
          {enviando ? (
            <ActivityIndicator color={tema.onPrimary} />
          ) : (
            <Text style={{ fontWeight: '600', color: tema.onPrimary }}>Confirmar y enviar</Text>
          )}
        </Pressable>
        <Pressable
          onPress={onEliminar}
          disabled={enviando}
          accessibilityRole="button"
          style={{
            minHeight: 48,
            alignItems: 'center',
            justifyContent: 'center',
            borderRadius: 12,
            padding: 12,
            backgroundColor: tema.errorContainer,
            opacity: enviando ? 0.7 : 1,
          }}
        >
          <Text style={{ fontWeight: '600', color: tema.onErrorContainer }}>Eliminar</Text>
        </Pressable>
      </View>
    </View>
  );
}
