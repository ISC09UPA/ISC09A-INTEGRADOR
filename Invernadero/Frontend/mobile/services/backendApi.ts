const configuredBaseUrl = process.env.EXPO_PUBLIC_TELEMETRIA_API_URL?.trim();

export const backendApiEnabled = Boolean(configuredBaseUrl);

const baseUrl = configuredBaseUrl?.replace(/\/+$/, '');

export class BackendApiError extends Error {
  constructor(
    message: string,
    public readonly status?: number,
  ) {
    super(message);
    this.name = 'BackendApiError';
  }
}

export async function solicitarApi<T>(path: string, init?: RequestInit): Promise<T> {
  if (!baseUrl) {
    throw new BackendApiError(
      'Configura EXPO_PUBLIC_TELEMETRIA_API_URL para conectar con la API.',
    );
  }

  let response: Response;
  try {
    response = await fetch(`${baseUrl}${path}`, {
      ...init,
      headers: {
        Accept: 'application/json',
        ...(init?.body && !(init.body instanceof FormData)
          ? { 'Content-Type': 'application/json' }
          : {}),
        ...init?.headers,
      },
    });
  } catch (error) {
    const detail = error instanceof Error ? error.message : 'Error de red desconocido.';
    throw new BackendApiError(
      `No se pudo conectar con la API (${baseUrl}). Verifica la URL, la red y que el Backend esté iniciado. ${detail}`,
    );
  }

  if (!response.ok) {
    const contentType = response.headers.get('content-type') ?? '';
    let detail = `La API respondió HTTP ${response.status}.`;

    if (contentType.includes('application/json')) {
      const problem = (await response.json()) as {
        title?: string;
        detail?: string;
      };
      detail = problem.detail ?? problem.title ?? detail;
    } else {
      const text = await response.text();
      if (text.trim()) detail = text;
    }

    if (response.status === 401 || response.status === 403) {
      detail += ' La app móvil no debe contener la API key del servidor.';
    }

    throw new BackendApiError(detail, response.status);
  }

  return (await response.json()) as T;
}

export async function descargarCsvApi(path: string): Promise<string> {
  if (!baseUrl) {
    throw new BackendApiError(
      'Configura EXPO_PUBLIC_TELEMETRIA_API_URL para conectar con la API.',
    );
  }

  let response: Response;
  try {
    response = await fetch(`${baseUrl}${path}`, {
      headers: { Accept: 'text/csv' },
    });
  } catch (error) {
    const detail = error instanceof Error ? error.message : 'Error de red desconocido.';
    throw new BackendApiError(`No se pudo descargar el CSV. ${detail}`);
  }

  if (!response.ok) {
    throw new BackendApiError(
      `La API no pudo exportar el CSV (HTTP ${response.status}).`,
      response.status,
    );
  }

  return response.text();
}
