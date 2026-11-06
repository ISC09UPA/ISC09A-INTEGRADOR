/** Cuerpo de POST /api/productos. Contrato de la API. */
export interface CrearProductoDto {
  referencia: string;
  descripcion: string;
  unidadId: string;
  gerenteAprobadorId: string | null;
}
