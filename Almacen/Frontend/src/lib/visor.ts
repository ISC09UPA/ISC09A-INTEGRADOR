import { router } from 'expo-router';

import type { ArchivoLocal } from '@/models/archivo-local.model';
import { archivosService } from '@/services';

/**
 * Muestra lo que devolvió ArchivosService: los PDF en el visor de la app; lo demás (CSV) en la hoja de compartir.
 * En el navegador el servicio ya lo abrió en otra pestaña y llega null.
 */
export async function mostrarArchivo(archivo: ArchivoLocal | null) {
  if (!archivo) return;
  if (archivo.tipo === 'application/pdf') {
    router.push({ pathname: '/visor-pdf', params: { nombre: archivo.nombre } });
    return;
  }
  await archivosService.compartir(archivo);
}
