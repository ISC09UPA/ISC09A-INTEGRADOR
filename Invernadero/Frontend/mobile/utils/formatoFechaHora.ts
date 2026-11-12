
import type { ConfiguracionApp } from '../services/configuracionService';

export function formatearFecha(
  fecha: string | Date,
  formato: ConfiguracionApp['formatoFecha'],
): string {
  const valor = new Date(fecha);

  if (Number.isNaN(valor.getTime())) return 'Fecha no disponible';

  const dia = String(valor.getDate()).padStart(2, '0');
  const mes = String(valor.getMonth() + 1).padStart(2, '0');
  const anio = valor.getFullYear();

  switch (formato) {
    case 'MM/dd/yyyy':
      return `${mes}/${dia}/${anio}`;

    case 'yyyy-MM-dd':
      return `${anio}-${mes}-${dia}`;

    default:
      return `${dia}/${mes}/${anio}`;
  }
}

export function formatearHora(
  fecha: string | Date,
  formato: ConfiguracionApp['formatoHora'],
): string {
  const valor = new Date(fecha);

  if (Number.isNaN(valor.getTime())) return '--:--';

  return valor.toLocaleTimeString('es-MX', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: formato === '12',
  });
}
