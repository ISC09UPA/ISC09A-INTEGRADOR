import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { AppState } from 'react-native';
import { File } from 'expo-file-system';
import {
  RecordingPresets,
  getRecordingPermissionsAsync,
  requestRecordingPermissionsAsync,
  setAudioModeAsync,
  useAudioRecorder,
} from 'expo-audio';

import { servicioReconocimiento, type SesionReconocimiento } from './reconocimiento';
import { servicioEnvio } from './envio';
import {
  DURACION_MINIMA_MS,
  TIEMPO_ENVIADO_MS,
  type EstadoVoz,
  type Grabacion,
} from './tipos';

/**
 * Opciones de grabación orientadas a voz: mono, 16 kHz, AAC (.m4a).
 * El formato definitivo lo decidirá quien integre Azure Speech (que puede
 * preferir WAV/PCM 16 kHz); basta con cambiarlo aquí.
 */
const OPCIONES_GRABACION = {
  ...RecordingPresets.HIGH_QUALITY,
  sampleRate: 16000,
  numberOfChannels: 1,
  bitRate: 64000,
};

interface EstadoGesto {
  zonaVisible: boolean;
  zonaActiva: boolean;
}

interface VozContexto {
  estado: EstadoVoz;
  /** Texto reconocido en vivo (provisional, todavía no definitivo). */
  transcripcionParcial: string;
  /** Grabación conservada durante revisión / envío. */
  grabacion: Grabacion | null;
  /** Marca de tiempo (Date.now) en que realmente empezó la grabación. */
  inicioMs: number | null;
  /** Estado visual del gesto de cancelar (zona roja con X). */
  gesto: EstadoGesto;
  /** true mientras se prepara el envío tras "Confirmar y enviar". */
  enviando: boolean;
  /** Mensaje breve y temporal para el usuario. */
  aviso: string | null;
  /** Indica qué partes siguen simuladas (para mostrarlo con honestidad en UI). */
  simulado: { reconocimiento: boolean; envio: boolean };

  /** Inicia una grabación. Devuelve false si se ignoró (ya hay una en curso, etc.). */
  iniciarGrabacion: () => boolean;
  /** Termina la grabación y pasa a revisión (o la descarta si fue demasiado corta). */
  finalizarGrabacion: () => Promise<void>;
  /** Descarta audio y transcripción y vuelve a idle. Nunca muestra la revisión. */
  cancelarGrabacion: () => Promise<void>;
  /** Lo usa el botón para reflejar el arrastre hacia la zona de cancelación. */
  actualizarGesto: (zonaVisible: boolean, zonaActiva: boolean) => void;
  confirmarYEnviar: () => Promise<void>;
  eliminarGrabacion: () => void;
  /** Vuelve a idle desde "enviado"/"procesando". */
  nuevaGrabacion: () => void;
}

const VozContext = createContext<VozContexto | null>(null);

export function useVoz(): VozContexto {
  const contexto = useContext(VozContext);
  if (!contexto) {
    throw new Error('useVoz debe usarse dentro de <VozProvider>.');
  }
  return contexto;
}

function borrarArchivo(uri: string | null | undefined): void {
  if (!uri) return;
  try {
    const archivo = new File(uri);
    if (archivo.exists) archivo.delete();
  } catch {
    // El archivo temporal pudo haber sido eliminado ya por el sistema.
  }
}

export function VozProvider({ children }: { children: ReactNode }) {
  const recorder = useAudioRecorder(OPCIONES_GRABACION);

  // ── Estado visible (UI) ───────────────────────────────────────────────
  const [estado, setEstado] = useState<EstadoVoz>('idle');
  const [transcripcionParcial, setParcial] = useState('');
  const [grabacion, setGrabacion] = useState<Grabacion | null>(null);
  const [inicioMs, setInicioMs] = useState<number | null>(null);
  const [gesto, setGesto] = useState<EstadoGesto>({ zonaVisible: false, zonaActiva: false });
  const [enviando, setEnviando] = useState(false);
  const [aviso, setAviso] = useState<string | null>(null);

  // ── Referencias síncronas (evitan dobles pulsaciones y estados obsoletos) ──
  const estadoRef = useRef<EstadoVoz>('idle');
  /** true desde que se pide iniciar hasta que se libera el recorder. */
  const ocupadoRef = useRef(false);
  /** true mientras se está finalizando/cancelando (evita cierres duplicados). */
  const cerrandoRef = useRef(false);
  const enviandoRef = useRef(false);
  const inicioRef = useRef(0);
  const arranqueRef = useRef<Promise<boolean>>(Promise.resolve(false));
  const reconocimientoRef = useRef<SesionReconocimiento | null>(null);
  const grabacionRef = useRef<Grabacion | null>(null);
  const gestoRef = useRef<EstadoGesto>({ zonaVisible: false, zonaActiva: false });
  const temporizadoresRef = useRef<{
    aviso?: ReturnType<typeof setTimeout>;
    proceso?: ReturnType<typeof setTimeout>;
  }>({});

  const cambiarEstado = useCallback((nuevo: EstadoVoz) => {
    estadoRef.current = nuevo;
    setEstado(nuevo);
  }, []);

  const avisar = useCallback((mensaje: string) => {
    setAviso(mensaje);
    clearTimeout(temporizadoresRef.current.aviso);
    temporizadoresRef.current.aviso = setTimeout(() => setAviso(null), 3500);
  }, []);

  const actualizarGesto = useCallback((zonaVisible: boolean, zonaActiva: boolean) => {
    const actual = gestoRef.current;
    if (actual.zonaVisible === zonaVisible && actual.zonaActiva === zonaActiva) return;
    gestoRef.current = { zonaVisible, zonaActiva };
    setGesto({ zonaVisible, zonaActiva });
  }, []);

  /** Vuelve a idle y libera todo. `borrar` elimina el audio conservado. */
  const restablecer = useCallback(
    (borrar: boolean) => {
      clearTimeout(temporizadoresRef.current.proceso);
      if (borrar) borrarArchivo(grabacionRef.current?.uri);
      grabacionRef.current = null;
      reconocimientoRef.current = null;
      enviandoRef.current = false;
      ocupadoRef.current = false;
      cerrandoRef.current = false;
      setGrabacion(null);
      setParcial('');
      setInicioMs(null);
      setEnviando(false);
      actualizarGesto(false, false);
      cambiarEstado('idle');
    },
    [actualizarGesto, cambiarEstado]
  );

  // ── Inicio ────────────────────────────────────────────────────────────
  const iniciarGrabacion = useCallback((): boolean => {
    const actual = estadoRef.current;

    if (actual === 'review') {
      avisar('Confirma o elimina la grabación actual antes de grabar otra.');
      return false;
    }
    if (actual === 'sent' || actual === 'processing') {
      avisar('Hay una solicitud en curso. Espera la respuesta del asistente.');
      return false;
    }
    // Evita dos grabaciones simultáneas y eventos duplicados.
    if (actual !== 'idle' || ocupadoRef.current) return false;

    ocupadoRef.current = true;
    cerrandoRef.current = false;
    setParcial('');
    setGrabacion(null);
    setInicioMs(null);
    cambiarEstado('recording');

    const fallar = (mensaje: string) => {
      if (estadoRef.current !== 'recording') return;
      restablecer(false);
      avisar(mensaje);
    };

    arranqueRef.current = (async () => {
      try {
        const permiso = await getRecordingPermissionsAsync();
        if (!permiso.granted) {
          const pedido = await requestRecordingPermissionsAsync();
          // El diálogo del sistema interrumpe el gesto: no se puede seguir
          // grabando con el dedo ya levantado, así que se pide repetir.
          fallar(
            pedido.granted
              ? 'Permiso concedido. Mantén presionado de nuevo para grabar.'
              : 'Activa el permiso del micrófono en los ajustes para poder grabar.'
          );
          return false;
        }

        await setAudioModeAsync({ allowsRecording: true, playsInSilentMode: true });
        await recorder.prepareToRecordAsync();
        recorder.record();

        inicioRef.current = Date.now();
        setInicioMs(inicioRef.current);
        reconocimientoRef.current = servicioReconocimiento.iniciar({ onParcial: setParcial });
        return true;
      } catch {
        fallar('No se pudo iniciar la grabación.');
        return false;
      }
    })();

    return true;
  }, [avisar, cambiarEstado, recorder, restablecer]);

  // ── Fin / cancelación ─────────────────────────────────────────────────
  const cerrar = useCallback(
    async (modo: 'finalizar' | 'cancelar') => {
      if (estadoRef.current !== 'recording' || cerrandoRef.current) return;
      cerrandoRef.current = true;

      // Si el usuario suelta antes de que termine de arrancar, se espera al arranque.
      const arranco = await arranqueRef.current;
      if (!arranco || estadoRef.current !== 'recording') return; // ya se restableció por un fallo

      const duracionMs = Date.now() - inicioRef.current;

      let textoFinal = '';
      if (modo === 'cancelar') {
        reconocimientoRef.current?.cancelar();
      } else {
        textoFinal = reconocimientoRef.current?.detener() ?? '';
      }
      reconocimientoRef.current = null;

      let uri: string | null = null;
      try {
        await recorder.stop();
        uri = recorder.uri;
      } catch {
        uri = recorder.uri;
      }
      // Devuelve la sesión de audio a modo reproducción (para el reproductor de revisión).
      await setAudioModeAsync({ allowsRecording: false, playsInSilentMode: true }).catch(
        () => undefined
      );

      if (modo === 'cancelar') {
        borrarArchivo(uri);
        restablecer(false);
        return;
      }

      if (duracionMs < DURACION_MINIMA_MS) {
        borrarArchivo(uri);
        restablecer(false);
        avisar('Grabación muy corta. Mantén presionado mientras hablas.');
        return;
      }

      const conservada: Grabacion = { uri, duracionMs, transcripcion: textoFinal };
      grabacionRef.current = conservada;
      setGrabacion(conservada);
      setParcial('');
      ocupadoRef.current = false;
      cerrandoRef.current = false;
      cambiarEstado('review');
    },
    [avisar, cambiarEstado, recorder, restablecer]
  );

  const finalizarGrabacion = useCallback(() => cerrar('finalizar'), [cerrar]);
  const cancelarGrabacion = useCallback(() => cerrar('cancelar'), [cerrar]);

  // ── Revisión / envío ──────────────────────────────────────────────────
  const confirmarYEnviar = useCallback(async () => {
    const conservada = grabacionRef.current;
    if (estadoRef.current !== 'review' || !conservada || enviandoRef.current) return;

    enviandoRef.current = true;
    setEnviando(true);
    try {
      await servicioEnvio.enviar({
        uri: conservada.uri,
        duracionMs: conservada.duracionMs,
        transcripcion: conservada.transcripcion,
      });
      cambiarEstado('sent');
      temporizadoresRef.current.proceso = setTimeout(() => {
        if (estadoRef.current === 'sent') cambiarEstado('processing');
      }, TIEMPO_ENVIADO_MS);
    } catch {
      avisar('No se pudo preparar el envío. Inténtalo de nuevo.');
    } finally {
      enviandoRef.current = false;
      setEnviando(false);
    }
  }, [avisar, cambiarEstado]);

  const eliminarGrabacion = useCallback(() => {
    if (estadoRef.current !== 'review' || enviandoRef.current) return;
    restablecer(true);
  }, [restablecer]);

  const nuevaGrabacion = useCallback(() => {
    if (estadoRef.current !== 'sent' && estadoRef.current !== 'processing') return;
    restablecer(true);
  }, [restablecer]);

  // ── Ciclo de vida ─────────────────────────────────────────────────────
  // Si la app pasa a segundo plano mientras se graba, se cierra la grabación
  // (queda en revisión) en lugar de dejar el micrófono abierto.
  useEffect(() => {
    const suscripcion = AppState.addEventListener('change', (siguiente) => {
      if (siguiente === 'background' && estadoRef.current === 'recording') {
        void cerrar('finalizar');
      }
    });
    return () => suscripcion.remove();
  }, [cerrar]);

  // Limpieza al desmontar: sin micrófono abierto ni archivos temporales huérfanos.
  useEffect(() => {
    const temporizadores = temporizadoresRef.current;
    return () => {
      clearTimeout(temporizadores.aviso);
      clearTimeout(temporizadores.proceso);
      reconocimientoRef.current?.cancelar();
      reconocimientoRef.current = null;
      if (estadoRef.current === 'recording') {
        try {
          const uri = recorder.uri;
          recorder.stop().then(() => borrarArchivo(uri ?? recorder.uri), () => undefined);
        } catch {
          // El recorder pudo haberse liberado ya.
        }
      }
      borrarArchivo(grabacionRef.current?.uri);
    };
  }, [recorder]);

  const valor = useMemo<VozContexto>(
    () => ({
      estado,
      transcripcionParcial,
      grabacion,
      inicioMs,
      gesto,
      enviando,
      aviso,
      simulado: {
        reconocimiento: servicioReconocimiento.esSimulado,
        envio: servicioEnvio.esSimulado,
      },
      iniciarGrabacion,
      finalizarGrabacion,
      cancelarGrabacion,
      actualizarGesto,
      confirmarYEnviar,
      eliminarGrabacion,
      nuevaGrabacion,
    }),
    [
      estado,
      transcripcionParcial,
      grabacion,
      inicioMs,
      gesto,
      enviando,
      aviso,
      iniciarGrabacion,
      finalizarGrabacion,
      cancelarGrabacion,
      actualizarGesto,
      confirmarYEnviar,
      eliminarGrabacion,
      nuevaGrabacion,
    ]
  );

  return <VozContext.Provider value={valor}>{children}</VozContext.Provider>;
}
