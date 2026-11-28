/** Línea de un documento registrado. Contrato de la API. */
export interface LineaDocumentoDto {
  productoId: string;
  referencia: string;
  descripcion: string;
  unidad: string;
  almacen?: string | null;
  cantidad: number;
  precioUnitario: number;
  tasaIva: number;
  subtotal: number;
  iva: number;
  costoUnitario: number;
  total: number;
  /** UID de la etiqueta NFC de cada pieza vendida (solo ventas del modo inteligente). */
  piezas?: string[] | null;
}
