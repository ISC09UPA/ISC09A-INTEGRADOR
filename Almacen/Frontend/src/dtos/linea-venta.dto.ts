/** Línea de POST /api/ventas. Contrato de la API. */
export interface LineaVentaDto {
  productoId: string;
  almacenId: string;
  cantidad: number;
  precioUnitario: number;
  tasaIva: number;
}
