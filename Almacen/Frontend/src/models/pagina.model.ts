/** Página de resultados de un listado. */
export interface Pagina<T> {
  elementos: T[];
  total: number;
  numeroPagina: number;
  tamano: number;
}
