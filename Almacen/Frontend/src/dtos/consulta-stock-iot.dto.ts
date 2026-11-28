import type { NivelStock } from '@/types/enums';

/** Comprobación de stock hecha desde el lector del embebido. Contrato de la API. */
export interface ConsultaStockIotDto {
  id: string;
  fecha: string;
  uid: string;
  familia: string;
  producto?: string | null;
  existencia?: number | null;
  minimo?: number | null;
  nivel?: NivelStock | null;
  motivo: string;
}
