import { ScrollView, Text, View } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';

import { EstadoEnvio } from '@/components/voz/EstadoEnvio';
import { EstadoEspera } from '@/components/voz/EstadoEspera';
import { IndicadorGrabacion, OndasAudio } from '@/components/voz/IndicadorGrabacion';
import { RevisionGrabacion } from '@/components/voz/RevisionGrabacion';
import { Transcripcion } from '@/components/voz/Transcripcion';
import { useTema } from '@/components/voz/useTema';
import { useVoz } from '@/voz/VozProvider';

/**
 * Pestaña «Voz»: muestra y controla todo el proceso de grabación.
 * La grabación se inicia con el botón global de micrófono (barra de pestañas);
 * esta pantalla solo refleja el estado compartido en VozProvider.
 */
export default function Voz() {
  const tema = useTema();
  const {
    estado,
    transcripcionParcial,
    grabacion,
    inicioMs,
    gesto,
    enviando,
    aviso,
    simulado,
    confirmarYEnviar,
    eliminarGrabacion,
    nuevaGrabacion,
  } = useVoz();

  const sobreZona = gesto.zonaActiva;

  return (
    <View style={{ flex: 1, backgroundColor: tema.surface }}>
      {aviso ? (
        <View
          accessibilityLiveRegion="polite"
          style={{
            margin: 16,
            marginBottom: 0,
            padding: 12,
            borderRadius: 12,
            backgroundColor: tema.tertiaryContainer,
          }}
        >
          <Text style={{ fontSize: 14, color: tema.onTertiaryContainer }}>{aviso}</Text>
        </View>
      ) : null}

      <ScrollView
        contentContainerStyle={{ flexGrow: 1, justifyContent: 'center', padding: 16, paddingBottom: 48 }}
        showsVerticalScrollIndicator={false}
        // Durante la grabación no hay nada que desplazar: evita roces con el gesto.
        scrollEnabled={estado !== 'recording'}
      >
        {estado === 'idle' ? <EstadoEspera /> : null}

        {estado === 'recording' ? (
          <View style={{ gap: 20 }}>
            <IndicadorGrabacion inicioMs={inicioMs} />
            <OndasAudio activo />

            <View style={{ alignItems: 'center', gap: 4 }}>
              <Text style={{ fontSize: 18, fontWeight: '600', color: tema.onSurface }}>
                Te estamos escuchando...
              </Text>
            </View>

            <Transcripcion
              texto={transcripcionParcial}
              enProgreso
              simulada={simulado.reconocimiento}
            />

            <View
              style={{
                flexDirection: 'row',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 8,
                paddingVertical: 10,
                paddingHorizontal: 14,
                borderRadius: 12,
                backgroundColor: sobreZona ? tema.errorContainer : tema.surfaceContainerLow,
              }}
            >
              <MaterialIcons
                name={sobreZona ? 'close' : 'pan-tool'}
                size={20}
                color={sobreZona ? tema.onErrorContainer : tema.onSurfaceVariant}
              />
              <Text
                style={{
                  fontSize: 14,
                  fontWeight: '600',
                  color: sobreZona ? tema.onErrorContainer : tema.onSurfaceVariant,
                }}
              >
                {sobreZona ? 'Suelta para cancelar la grabación' : 'Suelta el botón para terminar'}
              </Text>
            </View>
          </View>
        ) : null}

        {estado === 'review' && grabacion ? (
          <RevisionGrabacion
            grabacion={grabacion}
            enviando={enviando}
            transcripcionSimulada={simulado.reconocimiento}
            onConfirmar={() => void confirmarYEnviar()}
            onEliminar={eliminarGrabacion}
          />
        ) : null}

        {estado === 'sent' || estado === 'processing' ? (
          <EstadoEnvio
            fase={estado}
            grabacion={grabacion}
            envioSimulado={simulado.envio}
            transcripcionSimulada={simulado.reconocimiento}
            onNueva={nuevaGrabacion}
          />
        ) : null}
      </ScrollView>
    </View>
  );
}
