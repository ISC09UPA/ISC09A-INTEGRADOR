import { File } from 'expo-file-system';
import { solicitarApi } from '../../services/backendApi';

export interface GrabacionParaEnviar {
  /** URI local del archivo de audio (m4a/AAC según el preset del recorder). */
  uri: string | null;
  duracionMs: number;
  transcripcion: string;
}

export interface ServicioEnvio {
  /** true mientras no exista integración real con el backend. */
  readonly esSimulado: boolean;
  /** Debe rechazar la promesa si el envío falla. */
  enviar: (grabacion: GrabacionParaEnviar) => Promise<void>;
}

interface RespuestaSpeech {
  status: 'received';
  azureSpeechConfigured: false;
  persisted: false;
  message: string;
}

export const envioBackend: ServicioEnvio = {
  esSimulado: false,
  async enviar(grabacion) {
    if (!grabacion.uri) {
      throw new Error('No se encontró el archivo de audio de la grabación.');
    }

    const audio = new File(grabacion.uri);
    if (!audio.exists || audio.size === 0) {
      throw new Error('El archivo de audio ya no está disponible. Vuelve a grabar.');
    }

    const formulario = new FormData();
    formulario.append('audio', audio, audio.name || 'grabacion.m4a');
    formulario.append('transcription', grabacion.transcripcion);

    const respuesta = await solicitarApi<RespuestaSpeech>('/api/speech/receive', {
      method: 'POST',
      body: formulario,
    });

    if (
      respuesta.status !== 'received' ||
      respuesta.azureSpeechConfigured ||
      respuesta.persisted
    ) {
      throw new Error('La API devolvió un estado inesperado para la solicitud Speech.');
    }
  },
};

export const servicioEnvio: ServicioEnvio = envioBackend;
