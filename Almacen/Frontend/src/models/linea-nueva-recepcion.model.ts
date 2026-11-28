/** Línea de POST /api/recepciones. */
export interface LineaNuevaRecepcion {
  productoId: string;
  cantidad: number;
  costoUnitario: number;
  tasaIva: number;
}
