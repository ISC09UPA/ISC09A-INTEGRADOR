/** Página de resultados de un listado. Contrato de la API. */
export interface PaginaDto<T> {
  elementos: T[];
  total: number;
  numeroPagina: number;
  tamano: number;
}
