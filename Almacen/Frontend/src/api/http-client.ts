import Constants from 'expo-constants';
import { Platform } from 'react-native';

/**
 * Dirección de la API. Se toma de EXPO_PUBLIC_API_URL (archivo .env); si no está:
 * - en el navegador y el simulador de iOS, localhost;
 * - en el emulador de Android, 10.0.2.2 (el localhost de la PC);
 * - en un teléfono con Expo Go, la IP de la PC que sirve Metro (la API debe escuchar en 0.0.0.0).
 */
function urlPorDefecto(): string {
  const puerto = 5228;
  if (Platform.OS === 'web') return `http://localhost:${puerto}`;
  const host = Constants.expoConfig?.hostUri?.split(':')[0];
  if (host && host !== 'localhost' && host !== '127.0.0.1') return `http://${host}:${puerto}`;
  return Platform.OS === 'android' ? `http://10.0.2.2:${puerto}` : `http://localhost:${puerto}`;
}

export const API_URL = (process.env.EXPO_PUBLIC_API_URL || urlPorDefecto()).replace(/\/+$/, '');

/** Error de la API con el mensaje en español que manda el backend (ProblemDetails). */
export class ErrorApi extends Error {
  constructor(
    message: string,
    public readonly estado: number,
    public readonly errores?: Record<string, string[]>,
  ) {
    super(message);
  }
}

export type Parametros = Record<string, string | number | boolean | null | undefined>;
type Metodo = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

/**
 * Única puerta a la API: arma la URL, agrega el token, traduce errores y devuelve el DTO tal cual llega.
 * Los services convierten esos DTO a models con los mappers.
 */
export class HttpClient {
  private token: string | null = null;
  private alExpirar: (() => void) | null = null;

  constructor(private readonly baseUrl: string) {}

  /** Token de la sesión actual y qué hacer si la API responde 401. */
  configurarToken(token: string | null, alExpirar?: () => void) {
    this.token = token;
    if (alExpirar) this.alExpirar = alExpirar;
  }

  get tokenActual() {
    return this.token;
  }

  get encabezadosAutorizacion(): Record<string, string> {
    return this.token ? { Authorization: `Bearer ${this.token}` } : {};
  }

  url(ruta: string, parametros?: Parametros) {
    const query = Object.entries(parametros ?? {})
      .filter(([, v]) => v !== undefined && v !== null && v !== '')
      .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`)
      .join('&');
    return `${this.baseUrl}${ruta}${query ? `?${query}` : ''}`;
  }

  get<TDto>(ruta: string, parametros?: Parametros) {
    return this.enviar<TDto>('GET', ruta, { parametros });
  }

  post<TDto = void>(ruta: string, cuerpo: unknown = {}) {
    return this.enviar<TDto>('POST', ruta, { cuerpo });
  }

  put<TDto = void>(ruta: string, cuerpo: unknown) {
    return this.enviar<TDto>('PUT', ruta, { cuerpo });
  }

  patch<TDto = void>(ruta: string, cuerpo: unknown) {
    return this.enviar<TDto>('PATCH', ruta, { cuerpo });
  }

  subir<TDto>(ruta: string, formulario: FormData) {
    return this.enviar<TDto>('POST', ruta, { formulario });
  }

  /** Respuesta binaria (PDF, CSV) para el navegador. */
  async blob(ruta: string, parametros?: Parametros): Promise<Blob> {
    const r = await this.fetch(this.url(ruta, parametros), { headers: this.encabezadosAutorizacion });
    if (!r.ok) throw await this.leerError(r);
    return r.blob();
  }

  private async enviar<TDto>(metodo: Metodo, ruta: string, opciones: { parametros?: Parametros; cuerpo?: unknown; formulario?: FormData }): Promise<TDto> {
    const encabezados: Record<string, string> = { Accept: 'application/json', ...this.encabezadosAutorizacion };
    let body: BodyInit | undefined;
    if (opciones.formulario) {
      body = opciones.formulario;
    } else if (opciones.cuerpo !== undefined && metodo !== 'GET') {
      encabezados['Content-Type'] = 'application/json';
      body = JSON.stringify(opciones.cuerpo);
    }
    const r = await this.fetch(this.url(ruta, opciones.parametros), { method: metodo, headers: encabezados, body });
    if (!r.ok) throw await this.leerError(r);
    if (r.status === 204) return undefined as TDto;
    const texto = await r.text();
    return (texto ? JSON.parse(texto) : undefined) as TDto;
  }

  private async fetch(url: string, init: RequestInit) {
    try {
      return await fetch(url, init);
    } catch {
      throw new ErrorApi(`No se pudo conectar con la API en ${this.baseUrl}. Revisa que esté encendida y en la misma red.`, 0);
    }
  }

  private async leerError(respuesta: Response): Promise<ErrorApi> {
    let mensaje = `Error ${respuesta.status}`;
    let errores: Record<string, string[]> | undefined;
    try {
      const cuerpo = await respuesta.json();
      errores = cuerpo.errores ?? cuerpo.errors;
      mensaje = cuerpo.detail || cuerpo.title || mensaje;
      if (errores) {
        const lista = Object.values(errores).flat();
        if (lista.length) mensaje = lista.join('\n');
      }
    } catch {
      // Sin cuerpo JSON: se queda el código.
    }
    if (respuesta.status === 401 && this.token) {
      mensaje = 'Tu sesión terminó. Vuelve a iniciar sesión.';
      this.alExpirar?.();
    } else if (respuesta.status === 403) {
      mensaje = 'No tienes permiso para hacer esto.';
    }
    return new ErrorApi(mensaje, respuesta.status, errores);
  }
}

export const httpClient = new HttpClient(API_URL);
