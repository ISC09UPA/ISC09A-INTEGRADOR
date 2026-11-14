import { Image } from 'expo-image';
import * as ImagePicker from 'expo-image-picker';
import { Platform, View } from 'react-native';

import type { Producto } from '@/models/producto.model';
import { productosService } from '@/services';
import { Icono } from '@/components/ui';
import { Radio } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';

/** Imagen del producto servida por /api/archivos (pide el token, así que no basta con un <img> normal). */
export function ImagenProducto({ ruta, tamano = 72 }: { ruta?: string | null; tamano?: number }) {
  const c = useColores();
  if (!ruta) {
    return (
      <View style={{ width: tamano, height: tamano, borderRadius: Radio, backgroundColor: c.background, borderColor: c.line, borderWidth: 1, alignItems: 'center', justifyContent: 'center' }}>
        <Icono nombre="image" color={c.textSecondary} />
      </View>
    );
  }
  return (
    <Image
      source={productosService.fuenteImagen(ruta)}
      style={{ width: tamano, height: tamano, borderRadius: Radio, backgroundColor: c.background }}
      contentFit="cover"
      transition={150}
    />
  );
}

const TAMANO_MAXIMO = 2 * 1024 * 1024;

/** Elige una foto de la galería y la sube (JPG, PNG o WebP, máx. 2 MB). Devuelve null si se canceló. */
export async function elegirYSubirImagen(productoId: string): Promise<Producto | null> {
  const r = await ImagePicker.launchImageLibraryAsync({ mediaTypes: ['images'], quality: 0.8, allowsEditing: true, aspect: [1, 1] });
  if (r.canceled || !r.assets?.length) return null;
  const a = r.assets[0];
  const tipo = a.mimeType ?? 'image/jpeg';
  const nombre = a.fileName ?? `producto.${tipo.split('/')[1] ?? 'jpg'}`;
  const blob = Platform.OS === 'web' ? (a.file ?? (await (await fetch(a.uri)).blob())) : undefined;
  if ((blob?.size ?? a.fileSize ?? 0) > TAMANO_MAXIMO) throw new Error('La imagen debe pesar máximo 2 MB.');
  return productosService.subirImagen(productoId, { nombre, tipo, uri: a.uri, blob });
}
