/** Cuerpo de PUT /api/productos/{id}. Contrato de la API. */
export interface DatosProductoDto {
  descripcion: string;
  unidadId: string;
  /** Familia NFC: null la conserva, "" la quita. */
  codigoNfc?: string | null;
}
