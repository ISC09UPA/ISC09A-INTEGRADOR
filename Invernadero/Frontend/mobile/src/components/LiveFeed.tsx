import { Image } from 'expo-image';
import { Text, View } from 'react-native';
import { useEffect, useState } from 'react';
import { IMG_VISTA_VIVA, PLANTAS_SEMILLA } from '@/mocks/datos';

const IMAGENES_ROTACION = [IMG_VISTA_VIVA, PLANTAS_SEMILLA[0].imagenUrl, PLANTAS_SEMILLA[1].imagenUrl];

export function LiveFeed({ online }: { online: boolean }) {
  const [marco, setMarco] = useState(0);

  useEffect(() => {
    if (!online) return;
    const t = setInterval(() => setMarco((m) => (m + 1) % IMAGENES_ROTACION.length), 1500);
    return () => clearInterval(t);
  }, [online]);

  return (
    <View className="relative h-64 w-full overflow-hidden rounded-2xl bg-surface-container-highest">
      {online ? (
        <>
          <Image source={{ uri: IMAGENES_ROTACION[marco] }} style={{ width: '100%', height: '100%' }} contentFit="cover" />
          {/* HUD de la cámara */}
          <View className="absolute left-3 top-3 flex-row items-center gap-1.5 rounded-full bg-black/50 px-2.5 py-1">
            <View className="h-1.5 w-1.5 rounded-full bg-red-500" />
            <Text className="text-label-sm text-white">CAM-02 RPi · En vivo</Text>
          </View>
          <View className="absolute bottom-3 right-3 rounded-full bg-black/50 px-2.5 py-1">
            <Text className="text-label-sm text-white">12.4 k lx · 27 °C</Text>
          </View>
          {/* Retícula */}
          <View className="pointer-events-none absolute inset-6 rounded-2xl border-2 border-primary-fixed/70" />
        </>
      ) : (
        <View className="flex-1 items-center justify-center gap-1 p-4">
          <Text className="text-title-sm text-on-surface" style={{ fontWeight: '600' }}>
            Sin señal de la cámara
          </Text>
          <Text className="text-body-sm text-on-surface-variant">
            La Raspberry Pi está offline. El visor reanuda el stream al reconectar.
          </Text>
        </View>
      )}
    </View>
  );
}
