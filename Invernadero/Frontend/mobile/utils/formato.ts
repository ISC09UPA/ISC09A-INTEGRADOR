
function horaCorta(iso: string): string {
  return new Date(iso).toLocaleTimeString('es-MX', { hour: '2-digit', minute: '2-digit' });
}

export function relativo(iso: string): string {
  const fecha = new Date(iso);
  const hoy = new Date();
  const diffMin = Math.round((hoy.getTime() - fecha.getTime()) / 60000);
  if (diffMin < 1) return 'Ahora mismo';
  if (diffMin < 60) return `Hace ${diffMin} min`;
  const mismoDia = fecha.toDateString() === hoy.toDateString();
  if (mismoDia) return `Hoy, ${horaCorta(iso)} h`;
  const ayer = new Date(hoy);
  ayer.setDate(hoy.getDate() - 1);
  if (fecha.toDateString() === ayer.toDateString()) return `Ayer, ${horaCorta(iso)} h`;
  const dia = fecha.toLocaleDateString('es-MX', { day: 'numeric', month: 'short' });
  return `${dia}, ${horaCorta(iso)} h`;
}

export function proximoRelativo(iso: string): string {
  const diffMin = Math.round((new Date(iso).getTime() - Date.now()) / 60000);
  if (diffMin <= 0) return 'Ahora';
  if (diffMin < 60) return `En ${diffMin} min`;
  const horas = Math.round((diffMin / 60) * 10) / 10;
  if (horas < 24) return `En ${String(horas).replace('.0', '')} h`;
  const dias = Math.round(horas / 24);
  return `En ${dias} día${dias === 1 ? '' : 's'}`;
}

export function etiquetaEstadoSalud(estado: string): { texto: string; color: 'primary' | 'tertiary' | 'error' } {
  switch (estado) {
    case 'sana':
    case 'vigorosa_sana':
      return { texto: 'Vigorosa y Sana', color: 'primary' };
    case 'estres_hidrico':
    case 'observar':
      return { texto: 'Estrés hídrico · Observar', color: 'tertiary' };
    default:
      return { texto: 'Hoja enferma / plaga', color: 'error' };
  }
}


// Dominios desde los que se permite cargar imágenes remotas.
const HOSTS_IMAGEN = new Set<string>(['lh3.googleusercontent.com']); // temporal: desaparecerá con los mocks

const urlApi = (process.env.EXPO_PUBLIC_TELEMETRIA_API_URL ?? '').trim();
const hostApi = /^https:\/\/([^/:?#]+)/i.exec(urlApi)?.[1]?.toLowerCase();

if (hostApi) HOSTS_IMAGEN.add(hostApi);

/** Devuelve la URL solo si es local o HTTPS de un dominio permitido; si no, undefined. */
export function urlImagenSegura(
  url: string | undefined | null,
): string | undefined {
  if (!url) return undefined;
  if (/^file:\/\//i.test(url)) return url;

  const coincidencia = /^https:\/\/([^/:?#@\s]+)(?::\d+)?(?:[/?#]|$)/i.exec(url);
  const host = coincidencia?.[1]?.toLowerCase();

  return host && HOSTS_IMAGEN.has(host) ? url : undefined;
}
