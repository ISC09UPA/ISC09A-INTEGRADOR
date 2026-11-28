/** Línea de POST /api/ventas. */
export interface LineaNuevaVenta {
  productoId: string;
  almacenId: string;
  cantidad: number;
  precioUnitario: number;
  tasaIva: number;
  /** UID de cada pieza escaneada en el modo inteligente. */
  piezas?: string[];
}
