import type { NivelStock } from '@/types/enums';

/** Comprobación de stock hecha desde el lector del embebido. */
export interface ConsultaStockIot {
  id: string;
  fecha: string;
  uid: string;
  familia: string;
  producto?: string;
  existencia?: number;
  minimo?: number;
  nivel?: NivelStock;
  motivo: string;
}
