/** Respuesta de GET /api/almacenes/mio. Contrato de la API. */
export interface MiAlmacenDto {
  almacenId?: string | null;
  almacen?: string | null;
  /** True para el superadmin: no tiene almacén y ve todos. */
  veTodos: boolean;
}
