/** Archivo (PDF o CSV) ya descargado en la caché del teléfono. */
export interface ArchivoLocal {
  uri: string;
  nombre: string;
  /** Tipo MIME, p. ej. application/pdf. */
  tipo: string;
}
