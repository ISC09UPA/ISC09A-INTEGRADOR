/** Lo que se envía en PUT /api/productos/{id}. */
export interface CambiosProducto {
  descripcion: string;
  unidadId: string;
  /** Familia NFC; vacía la quita. */
  codigoNfc: string;
  /** Código proveedor NFC; vacío lo quita. */
  codigoProveedor: string;
}
