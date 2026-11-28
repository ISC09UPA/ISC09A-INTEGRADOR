import type { FilaMesDto } from '@/dtos/fila-mes.dto';
import type { MovimientoFinancieroDto } from '@/dtos/movimiento-financiero.dto';
import type { ReporteMensualDto } from '@/dtos/reporte-mensual.dto';
import type { ResumenFinancieroDto } from '@/dtos/resumen-financiero.dto';
import type { ValoresAnioDto } from '@/dtos/valores-anio.dto';
import type { FilaMes } from '@/models/fila-mes.model';
import type { MovimientoFinanciero } from '@/models/movimiento-financiero.model';
import type { ReporteMensual } from '@/models/reporte-mensual.model';
import type { ResumenFinanciero } from '@/models/resumen-financiero.model';
import type { ValoresAnio } from '@/models/valores-anio.model';

export class FinanzasMapper {
  static movimiento(dto: MovimientoFinancieroDto): MovimientoFinanciero {
    return {
      id: dto.id,
      fecha: dto.fecha,
      naturaleza: dto.naturaleza,
      documento: { tipo: dto.documentoTipo, id: dto.documentoId, folio: dto.folio },
      tercero: dto.tercero,
      subtotal: dto.subtotal,
      iva: dto.iva,
      total: dto.total,
      esReverso: dto.esReverso,
    };
  }

  static resumen(dto: ResumenFinancieroDto): ResumenFinanciero {
    return {
      movimientos: dto.movimientos.map(FinanzasMapper.movimiento),
      total: dto.total,
      ingresos: dto.ingresos,
      egresos: dto.egresos,
      ivaTrasladado: dto.ivaTrasladado,
      ivaAcreditable: dto.ivaAcreditable,
      saldo: dto.saldo,
      ivaPorPagar: dto.ivaTrasladado - dto.ivaAcreditable,
    };
  }

  static valoresAnio(dto: ValoresAnioDto): ValoresAnio {
    return { anio: dto.anio, debe: dto.debe, haber: dto.haber };
  }

  static filaMes(dto: FilaMesDto): FilaMes {
    return { mes: dto.mes, nombre: dto.nombre, porAnio: dto.porAnio.map(FinanzasMapper.valoresAnio) };
  }

  static reporteMensual(dto: ReporteMensualDto): ReporteMensual {
    return {
      anios: dto.anios,
      meses: dto.meses.map(FinanzasMapper.filaMes),
      totales: dto.totales.map(FinanzasMapper.valoresAnio),
      saldoActual: dto.saldoActual,
    };
  }
}
