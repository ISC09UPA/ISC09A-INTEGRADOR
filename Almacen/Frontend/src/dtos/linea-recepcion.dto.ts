/** Línea de POST /api/recepciones. Contrato de la API. */
export interface LineaRecepcionDto {
  productoId: string;
  cantidad: number;
  costoUnitario: number;
  tasaIva: number;
}
