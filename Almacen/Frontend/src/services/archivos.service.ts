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

<<<<<<< HEAD
/**
 * PDF y CSV de la API. En el navegador se abren en otra pestaña (o se descargan);
 * en el teléfono se bajan a la caché y la app los muestra en su visor, con opciones de compartir y guardar.
 */
=======
/** Descarga PDF de la API (con el token) a la caché del teléfono, o a un blob en el navegador. */
>>>>>>> origin/7jaylosmolcafriends-almacen-develop
export class ArchivosService {
  constructor(private readonly http: HttpClient) {}

  comprobante(tipo: TipoDocumento, id: string, nombre: string) {
<<<<<<< HEAD
    return this.abrir(`${RUTA_COMPROBANTE[tipo]}/${id}`, undefined, nombre);
  }

  reporte(tipo: TipoReporte, p: Periodo) {
    return this.abrir(`/api/pdf/reportes/${tipo}`, { ...p }, `reporte-${tipo.toLowerCase()}-${p.desde}-${p.hasta}.pdf`);
  }

  reporteMensual(anio: number) {
    return this.abrir('/api/pdf/finanzas/mensual', { anio }, `reporte-mensual-${anio}.pdf`);
  }

  movimientosPdf(filtro: FiltroMovimientos) {
    return this.abrir('/api/pdf/movimientos', MovimientosService.parametros(filtro), `movimientos-${filtro.desde}-${filtro.hasta}.pdf`);
  }

  movimientosCsv(filtro: FiltroMovimientos) {
    return this.abrir('/api/movimientos/csv', MovimientosService.parametros(filtro), `movimientos-${filtro.desde}-${filtro.hasta}.csv`, 'text/csv');
  }

  /** En el teléfono devuelve el archivo descargado para mostrarlo en el visor; en el navegador lo abre en otra pestaña y devuelve null. */
  private async abrir(ruta: string, parametros: Parametros | undefined, nombre: string, tipo = 'application/pdf'): Promise<ArchivoLocal | null> {
    if (Platform.OS === 'web') {
      await this.abrirEnNavegador(ruta, parametros, nombre, tipo);
      return null;
    }

    const carpeta = ArchivosService.carpeta();
    if (!carpeta.exists) carpeta.create({ intermediates: true });
    try {
      await File.downloadFileAsync(this.http.url(ruta, parametros), new File(carpeta, nombre), {
        headers: this.http.encabezadosAutorizacion,
        idempotent: true,
      });
    } catch {
      throw new ErrorApi('No se pudo descargar el archivo. Revisa la conexión con la API y tus permisos.', 0);
    }
    return this.local(nombre, tipo);
  }

  /**
   * Un archivo ya descargado, por su nombre. Se rearma desde la carpeta de caché en vez de pasar la uri entre pantallas:
   * en Expo Go la ruta lleva caracteres codificados (%40, %2F) y al pasar por los parámetros de la ruta cambia y Android niega la lectura.
   */
  local(nombre: string, tipo = 'application/pdf'): ArchivoLocal {
    return { uri: new File(ArchivosService.carpeta(), nombre).uri, nombre, tipo };
  }

  /** Contenido en base64 (para dibujar el PDF con pdf.js en Android). */
  base64(archivo: ArchivoLocal) {
    return new File(ArchivosService.carpeta(), archivo.nombre).base64();
  }

  private static carpeta() {
    return new Directory(Paths.cache, 'documentos');
  }

  /** Hoja de compartir del sistema (WhatsApp, correo, Drive...). En iOS incluye "Guardar en Archivos". */
  async compartir(archivo: ArchivoLocal) {
    if (!(await Sharing.isAvailableAsync())) throw new ErrorApi('Este dispositivo no permite compartir archivos.', 0);
    await Sharing.shareAsync(this.local(archivo.nombre, archivo.tipo).uri, { mimeType: archivo.tipo, dialogTitle: archivo.nombre, UTI: archivo.tipo === 'application/pdf' ? 'com.adobe.pdf' : undefined });
  }

  /**
   * Guarda una copia donde el usuario elija. En Android abre el selector de carpeta (p. ej. Descargas);
   * en iOS no hay carpeta de descargas, así que se usa la hoja de compartir con "Guardar en Archivos".
   * Devuelve false si el usuario canceló.
   */
  async guardar(archivo: ArchivoLocal): Promise<boolean> {
    if (Platform.OS !== 'android') {
      await this.compartir(archivo);
      return true;
    }
    let carpeta: Directory;
    try {
      carpeta = await Directory.pickDirectoryAsync();
    } catch {
      return false;
    }
    const destino = carpeta.createFile(archivo.nombre, archivo.tipo);
    destino.write(await new File(ArchivosService.carpeta(), archivo.nombre).bytes());
    return true;
  }

  private async abrirEnNavegador(ruta: string, parametros: Parametros | undefined, nombre: string, tipo: string) {
    // La ventana se abre antes de pedir el archivo para que el navegador no la bloquee como emergente.
    const ventana = tipo === 'application/pdf' ? window.open('', '_blank') : null;
    try {
      const enlace = URL.createObjectURL(await this.http.blob(ruta, parametros));
      if (ventana) {
        ventana.location.href = enlace;
      } else {
        const a = document.createElement('a');
        a.href = enlace;
        a.download = nombre;
        a.click();
      }
      setTimeout(() => URL.revokeObjectURL(enlace), 60_000);
    } catch (e) {
      ventana?.close();
      throw e;
=======
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
>>>>>>> origin/7jaylosmolcafriends-almacen-develop
    }
  }
}
