import * as Sharing from 'expo-sharing';
import { Platform } from 'react-native';

import type { ArchivoLocal } from '@/models/archivo-local.model';

/** Muestra un archivo descargado: en el navegador lo abre en otra pestaña; en el teléfono, con el visor del sistema. */
export async function mostrarArchivo(archivo: ArchivoLocal) {
  if (Platform.OS === 'web') {
    globalThis.open?.(archivo.uri, '_blank');
    return;
  }
  if (!(await Sharing.isAvailableAsync())) throw new Error('Este dispositivo no puede abrir el archivo.');
  await Sharing.shareAsync(archivo.uri, { mimeType: archivo.tipo, dialogTitle: archivo.nombre, UTI: archivo.tipo === 'application/pdf' ? 'com.adobe.pdf' : undefined });
}
