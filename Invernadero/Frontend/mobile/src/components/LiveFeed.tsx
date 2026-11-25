import { useTema } from '@/hooks/useTema';
import { IMG_VISTA_VIVA, PLANTAS_SEMILLA } from '@/mocks/datos';
import { Image } from 'expo-image';
import { useEffect, useState } from 'react';
import { Text, View } from 'react-native';
import { urlImagenSegura } from '../../utils/formato';

const IMAGENES_ROTACION = [
  IMG_VISTA_VIVA,
  PLANTAS_SEMILLA[0].imagenUrl,
  PLANTAS_SEMILLA[1].imagenUrl,
];

export function LiveFeed({ online }: { online: boolean }) {
  const [marco, setMarco] = useState(0);
  const tema = useTema();
  const imagen = urlImagenSegura(IMAGENES_ROTACION[marco]);

  useEffect(() => {
    if (!online) return;

    const t = setInterval(
      () => setMarco((m) => (m + 1) % IMAGENES_ROTACION.length),
      1500
    );

    return () => clearInterval(t);
  }, [online]);

  return (
    <View
      className="relative h-64 w-full overflow-hidden rounded-2xl"
      style={{ backgroundColor: tema.surfaceContainerHighest }}
    >
      {online ? (
        <>
          {imagen && (
            <Image
              source={{ uri: imagen }}
              style={{ width: '100%', height: '100%' }}
              contentFit="cover"
            />
          )}

          {/* Etiqueta de cámara de demostración */}
          <View className="absolute left-3 top-3 flex-row items-center gap-1.5 rounded-full bg-black/50 px-2.5 py-1">
            <View className="h-1.5 w-1.5 rounded-full bg-red-500" />
            <Text className="text-label-sm text-white">
              CAM-02 · Demo
            </Text>
          </View>

          {/* Retícula */}
          <View
            pointerEvents="none"
            className="absolute inset-6 rounded-2xl border-2"
            style={{ borderColor: `${tema.primaryFixed}B3` }}
          />
        </>
      ) : (
        <View className="flex-1 items-center justify-center gap-1 p-4">
          <Text style={{ fontSize: 16, fontWeight: '600', color: tema.onSurface }}>
            Sin señal de la cámara
          </Text>

          <Text style={{ fontSize: 14, color: tema.onSurfaceVariant }}>
            La Raspberry Pi está offline. El visor reanuda el stream al reconectar.
          </Text>
        </View>
      )}
    </View>
  );
}