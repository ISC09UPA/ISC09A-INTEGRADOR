/** Línea de POST /api/ventas. Contrato de la API. */
export interface LineaVentaDto {
  productoId: string;
  almacenId: string;
  cantidad: number;
  precioUnitario: number;
  tasaIva: number;
  /** UID de la etiqueta NFC de cada pieza escaneada (a lo más la cantidad). */
  piezas?: string[] | null;
}
