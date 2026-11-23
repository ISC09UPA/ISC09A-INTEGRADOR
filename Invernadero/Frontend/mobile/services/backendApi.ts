const configuredBaseUrl = process.env.EXPO_PUBLIC_TELEMETRIA_API_URL?.trim();

export const backendApiEnabled = Boolean(configuredBaseUrl);

const baseUrl = configuredBaseUrl?.replace(/\/+$/, '');

const TIMEOUT_CONSULTA_MS = 15_000;
const TIMEOUT_SUBIDA_MS = 60_000;

export class BackendApiError extends Error {
  constructor(
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = 'BackendApiError';
  }
}

/** Petición con timeout y mensajes de error unificados. */
async function peticion(
  path: string,
  init: RequestInit | undefined,
  timeoutMs: number,
): Promise<Response> {
  if (!baseUrl) {
    throw new BackendApiError(
      'Configura EXPO_PUBLIC_TELEMETRIA_API_URL para conectar con la API.',
    );
  }

  const controlador = new AbortController();
  const temporizador = setTimeout(() => controlador.abort(), timeoutMs);

  try {
    return await fetch(`${baseUrl}${path}`, { ...init, signal: controlador.signal });
  } catch (error) {
    const detalle =
      error instanceof Error && error.name === 'AbortError'
        ? 'La API tardó demasiado en responder.'
        : error instanceof Error
          ? error.message
          : 'Error de red desconocido.';
    throw new BackendApiError(
      `No se pudo conectar con la API (${baseUrl}). Verifica la URL, la red y que el Backend esté iniciado. ${detalle}`,
    );
  } finally {
    clearTimeout(temporizador);
  }
}

/** Extrae un mensaje legible de una respuesta de error (sin lanzar si el cuerpo no es válido). */
async function detalleDeError(response: Response): Promise<string> {
  let detalle = `La API respondió HTTP ${response.status}.`;
  const contentType = response.headers.get('content-type') ?? '';

  try {
    if (contentType.includes('application/json')) {
      const problema = (await response.json()) as { title?: string; detail?: string } | null;
      detalle = problema?.detail ?? problema?.title ?? detalle;
    } else {
      const texto = await response.text();
      if (texto.trim()) detalle = texto;
    }
  } catch {
    // Cuerpo ilegible: se conserva el mensaje con el código HTTP.
  }

  if (response.status === 401 || response.status === 403) {
    detalle += ' La app móvil no debe contener la API key del servidor.';
  }
  return detalle;
}

export async function solicitarApi<T>(path: string, init?: RequestInit): Promise<T> {
  const esSubida = init?.body instanceof FormData;

  const response = await peticion(
    path,
    {
      ...init,
      headers: {
        Accept: 'application/json',
        ...(init?.body && !esSubida ? { 'Content-Type': 'application/json' } : {}),
        ...init?.headers,
      },
    },
    esSubida ? TIMEOUT_SUBIDA_MS : TIMEOUT_CONSULTA_MS,
  );

  if (!response.ok) {
    throw new BackendApiError(await detalleDeError(response), response.status);
  }

  try {
    return (await response.json()) as T;
  } catch {
    throw new BackendApiError(
      'La API devolvió una respuesta que no se pudo leer.',
      response.status,
    );
  }
}

export async function descargarCsvApi(path: string): Promise<string> {
  const response = await peticion(
    path,
    { headers: { Accept: 'text/csv' } },
    TIMEOUT_CONSULTA_MS,
  );

  if (!response.ok) {
    throw new BackendApiError(
      `La API no pudo exportar el CSV (HTTP ${response.status}).`,
      response.status,
    );
  }

  return response.text();
}