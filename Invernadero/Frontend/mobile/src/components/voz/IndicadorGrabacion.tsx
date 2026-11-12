import { useEffect, useState } from 'react';
import { Animated, Text, View } from 'react-native';

import { formatearDuracion } from '@/voz/formato';
import { useTema } from './useTema';

function Cronometro({ inicioMs }: { inicioMs: number | null }) {
  const tema = useTema();
  const [transcurrido, setTranscurrido] = useState(0);

  useEffect(() => {
    if (inicioMs === null) return;
    const actualizar = () => setTranscurrido(Date.now() - inicioMs);
    const id = setInterval(actualizar, 250);
    return () => clearInterval(id);
  }, [inicioMs]);

  return (
    <Text
      accessibilityLabel={`Duración ${formatearDuracion(inicioMs === null ? 0 : transcurrido)}`}
      style={{
        fontSize: 44,
        fontWeight: '300',
        fontVariant: ['tabular-nums'],
        color: tema.onSurface,
      }}
    >
      {formatearDuracion(inicioMs === null ? 0 : transcurrido)}
    </Text>
  );
}

const BARRAS = 21;

/** Ondas de audio animadas (indicador visual de actividad, no reflejan el nivel real). */
export function OndasAudio({ activo }: { activo: boolean }) {
  const tema = useTema();
  const [barras] = useState(() =>
    Array.from({ length: BARRAS }, () => new Animated.Value(0.25))
  );

  useEffect(() => {
    if (!activo) return;
    const animaciones = barras.map((valor, i) => {
      // Duraciones deterministas distintas por barra para un movimiento orgánico.
      const duracion = 320 + ((i * 97) % 7) * 70;
      return Animated.loop(
        Animated.sequence([
          Animated.timing(valor, { toValue: 1, duration: duracion, useNativeDriver: true }),
          Animated.timing(valor, { toValue: 0.25, duration: duracion, useNativeDriver: true }),
        ])
      );
    });
    animaciones.forEach((a) => a.start());
    return () => animaciones.forEach((a) => a.stop());
  }, [activo, barras]);

  return (
    <View
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
      style={{ height: 56, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 4 }}
    >
      {barras.map((valor, i) => (
        <Animated.View
          key={i}
          style={{
            width: 4,
            height: 56,
            borderRadius: 2,
            backgroundColor: tema.error,
            transform: [{ scaleY: valor }],
          }}
        />
      ))}
    </View>
  );
}

/** Indicador rojo «GRABANDO» + cronómetro. */
export function IndicadorGrabacion({ inicioMs }: { inicioMs: number | null }) {
  const tema = useTema();
  const [parpadeo] = useState(() => new Animated.Value(1));

  useEffect(() => {
    const animacion = Animated.loop(
      Animated.sequence([
        Animated.timing(parpadeo, { toValue: 0.2, duration: 600, useNativeDriver: true }),
        Animated.timing(parpadeo, { toValue: 1, duration: 600, useNativeDriver: true }),
      ])
    );
    animacion.start();
    return () => animacion.stop();
  }, [parpadeo]);

  return (
    <View style={{ alignItems: 'center', gap: 6 }}>
      <View
        accessibilityRole="text"
        accessibilityLabel="Grabando"
        style={{
          flexDirection: 'row',
          alignItems: 'center',
          gap: 8,
          paddingHorizontal: 14,
          paddingVertical: 6,
          borderRadius: 999,
          backgroundColor: tema.error,
        }}
      >
        <Animated.View
          style={{
            width: 10,
            height: 10,
            borderRadius: 5,
            backgroundColor: tema.onError,
            opacity: parpadeo,
          }}
        />
        <Text style={{ fontSize: 13, fontWeight: '700', letterSpacing: 1.2, color: tema.onError }}>
          GRABANDO
        </Text>
      </View>
      <Cronometro inicioMs={inicioMs} />
    </View>
  );
}
