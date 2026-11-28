import { Directory, File, Paths } from 'expo-file-system';
import { Platform } from 'react-native';

import { ErrorApi, HttpClient } from '@/api/http-client';
import type { ArchivoLocal } from '@/models/archivo-local.model';
import type { TipoDocumento } from '@/types/enums';

const RUTAS: Record<TipoDocumento, string> = {
  Venta: '/api/pdf/ventas',
  Recepcion: '/api/pdf/recepciones',
  Traspaso: '/api/pdf/traspasos',
};

/** Descarga PDF de la API (con el token) a la caché del teléfono, o a un blob en el navegador. */
export class ArchivosService {
  constructor(private readonly http: HttpClient) {}

  comprobante(tipo: TipoDocumento, id: string, nombre: string) {
    return this.descargar(`${RUTAS[tipo]}/${id}`, nombre, 'application/pdf');
  }

  private async descargar(ruta: string, nombre: string, tipo: string): Promise<ArchivoLocal> {
    if (Platform.OS === 'web') {
      const blob = await this.http.blob(ruta);
      return { uri: URL.createObjectURL(blob), nombre, tipo };
    }
    const carpeta = new Directory(Paths.cache, 'archivos');
    if (!carpeta.exists) carpeta.create({ intermediates: true });
    const destino = new File(carpeta, nombre);
    if (destino.exists) destino.delete();
    try {
      const archivo = await File.downloadFileAsync(this.http.url(ruta), destino, { headers: this.http.encabezadosAutorizacion });
      return { uri: archivo.uri, nombre, tipo };
    } catch (e) {
      throw new ErrorApi(e instanceof Error ? `No se pudo descargar ${nombre}: ${e.message}` : `No se pudo descargar ${nombre}.`, 0);
    }
  }
}
