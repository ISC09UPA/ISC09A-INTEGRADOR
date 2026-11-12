/**
 * ──────────────────────────────────────────────────────────────────────────
 *  PUNTO DE INTEGRACIÓN: RECONOCIMIENTO DE VOZ (Azure Speech Service)
 * ──────────────────────────────────────────────────────────────────────────
 *  Grabar un archivo NO genera texto. El texto debe venir de un servicio de
 *  reconocimiento. Mientras el backend / Azure no estén integrados se usa
 *  `reconocimientoSimulado`, que emite texto de PRUEBA (no es reconocimiento
 *  real) únicamente para poder validar visualmente la transcripción en vivo.
 *
 *  Para integrar el servicio real:
 *    1. Implementa `ServicioReconocimiento` (por ejemplo, abriendo un
 *       WebSocket hacia el backend que reenvíe el audio a Azure Speech).
 *    2. Llama a `eventos.onParcial(texto)` cada vez que llegue un resultado
 *       parcial ("recognizing").
 *    3. Devuelve el texto definitivo ("recognized") en `detener()`.
 *    4. Cambia la exportación `servicioReconocimiento` al final de este archivo.
 */

export interface EventosReconocimiento {
  /** Se invoca con el texto parcial acumulado conforme llega el reconocimiento. */
  onParcial: (texto: string) => void;
}

export interface SesionReconocimiento {
  /** Finaliza el reconocimiento y devuelve el texto final. */
  detener: () => string;
  /** Aborta el reconocimiento y descarta cualquier texto. */
  cancelar: () => void;
}

export interface ServicioReconocimiento {
  /** true mientras el texto sea de prueba y no provenga de un reconocimiento real. */
  readonly esSimulado: boolean;
  iniciar: (eventos: EventosReconocimiento) => SesionReconocimiento;
}

// ── Implementación SIMULADA (solo para pruebas visuales) ───────────────────
const PALABRAS_DE_PRUEBA =
  'Esto es una transcripción de prueba generada en el dispositivo mientras no exista el reconocimiento de voz real de Azure'.split(
    ' '
  );
const INTERVALO_MS = 650;

export const reconocimientoSimulado: ServicioReconocimiento = {
  esSimulado: true,
  iniciar: ({ onParcial }) => {
    let indice = 0;
    let texto = '';
    const temporizador = setInterval(() => {
      if (indice >= PALABRAS_DE_PRUEBA.length) return;
      texto = texto ? `${texto} ${PALABRAS_DE_PRUEBA[indice]}` : PALABRAS_DE_PRUEBA[indice];
      indice += 1;
      onParcial(texto);
    }, INTERVALO_MS);

    return {
      detener: () => {
        clearInterval(temporizador);
        return texto;
      },
      cancelar: () => {
        clearInterval(temporizador);
        texto = '';
      },
    };
  },
};

/** Servicio activo. Sustituir por la implementación real cuando exista. */
export const servicioReconocimiento: ServicioReconocimiento = reconocimientoSimulado;
