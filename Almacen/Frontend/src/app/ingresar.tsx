import { useCallback, useEffect, useRef, useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';

import { API_URL } from '@/api/http-client';
import { Aviso, Boton, Icono, Texto, mensajeDe } from '@/components/ui';
import { Radio, Spacing } from '@/constants/theme';
import { useColores } from '@/hooks/use-colores';
import { useSesion } from '@/lib/sesion';
import type { SolicitudInicioSesionIot } from '@/models/inicio-sesion-iot.model';
import { cuentaService } from '@/services';

/** Cada cuánto se pregunta a la API si ya pasó alguien por el lector. */
const SONDEO_MS = 1000;
/** Margen tras la cuenta regresiva antes de darla por vencida (el reloj del teléfono y el del servidor no coinciden). */
const MARGEN_S = 3;

type Paso =
  | { tipo: 'inicio' }
  | { tipo: 'pidiendo' }
  | { tipo: 'esperando'; solicitud: SolicitudInicioSesionIot; inicio: number }
  | { tipo: 'error'; mensaje: string };

/**
 * Inicio de sesión con el lector del almacén: la persona acerca su tarjeta NFC y mira a la cámara del embebido.
 * Ya no hay correo ni contraseña en la app; los usuarios, roles y permisos son los mismos que en la web.
 */
export default function Ingresar() {
  const c = useColores();
  const { ingresar } = useSesion();
  const [paso, setPaso] = useState<Paso>({ tipo: 'inicio' });
  const [restante, setRestante] = useState(0);
  const consultando = useRef(false);
  const pendiente = useRef<string | null>(null);

  const comenzar = async () => {
    setPaso({ tipo: 'pidiendo' });
    try {
      const solicitud = await cuentaService.iniciarConLector();
      pendiente.current = solicitud.id;
      setRestante(solicitud.vigenciaSegundos);
      setPaso({ tipo: 'esperando', solicitud, inicio: Date.now() });
    } catch (e) {
      setPaso({ tipo: 'error', mensaje: mensajeDe(e) });
    }
  };

  const cancelar = useCallback(async () => {
    const id = pendiente.current;
    pendiente.current = null;
    setPaso({ tipo: 'inicio' });
    if (id) await cuentaService.cancelarLector(id).catch(() => undefined);
  }, []);

  // Sondeo cada segundo mientras la solicitud está pendiente.
  useEffect(() => {
    if (paso.tipo !== 'esperando') return;
    const { solicitud, inicio } = paso;
    const t = setInterval(async () => {
      const segundos = solicitud.vigenciaSegundos - Math.floor((Date.now() - inicio) / 1000);
      setRestante(Math.max(0, segundos));
      if (consultando.current) return;
      consultando.current = true;
      try {
        const r = await cuentaService.consultarLector(solicitud.id);
        if (pendiente.current !== solicitud.id) return; // Se canceló mientras llegaba la respuesta.
        if (r.estado === 'completada') {
          pendiente.current = null;
          ingresar(r.sesion);
        } else if (r.estado !== 'pendiente') {
          pendiente.current = null;
          setPaso({ tipo: 'error', mensaje: r.motivo });
        } else if (segundos < -MARGEN_S) {
          pendiente.current = null;
          setPaso({ tipo: 'error', mensaje: 'Se acabó el tiempo. Intenta de nuevo.' });
        }
      } catch (e) {
        // Un tropiezo de red no cancela: se sigue intentando hasta que venza.
        if (segundos < -MARGEN_S) {
          pendiente.current = null;
          setPaso({ tipo: 'error', mensaje: mensajeDe(e) });
        }
      } finally {
        consultando.current = false;
      }
    }, SONDEO_MS);
    return () => clearInterval(t);
  }, [paso, ingresar]);

  // Si se sale de la pantalla con una solicitud pendiente, se libera el lector para otra persona.
  useEffect(
    () => () => {
      if (pendiente.current) void cuentaService.cancelarLector(pendiente.current).catch(() => undefined);
    },
    [],
  );

  return (
    <ScrollView style={{ backgroundColor: c.drawer }} contentContainerStyle={estilos.fondo}>
      <View style={[estilos.tarjeta, { backgroundColor: c.surface }]}>
        <View style={estilos.marca}>
          <View style={[estilos.icono, { backgroundColor: c.primary }]}>
            <Text style={{ color: c.onPrimary, fontWeight: '700' }}>AI</Text>
          </View>
          <Texto tipo="fuerte">Almacén inteligente</Texto>
        </View>

        {paso.tipo === 'esperando' ? (
          <View style={{ gap: Spacing.three, alignItems: 'center' }}>
            <View style={estilos.pictogramas}>
              <Icono nombre="nfc" tamano={56} color={c.primary} />
              <Icono nombre="face" tamano={56} color={c.primary} />
            </View>
            <Text style={[estilos.instruccion, { color: c.text }]} accessibilityRole="header">
              Acerca tu tarjeta al lector y mira a la cámara
            </Text>
            <Texto tipo="secundario" style={{ textAlign: 'center' }}>
              En cuanto el lector te reconozca entrarás a la app.
            </Texto>
            <Text style={[estilos.cuenta, { color: restante <= 10 ? c.error : c.primary }]} accessibilityLabel={`Quedan ${restante} segundos`}>
              {restante}
            </Text>
            <Texto tipo="pequeno">segundos</Texto>
            <Boton texto="Cancelar" variante="contorno" icono="close" alPresionar={cancelar} style={{ alignSelf: 'stretch' }} />
          </View>
        ) : (
          <View style={{ gap: Spacing.three }}>
            <Texto tipo="titulo">Iniciar sesión</Texto>
            <Texto tipo="secundario">Usa tu tarjeta y tu rostro en el lector del almacén. No necesitas contraseña.</Texto>
            {paso.tipo === 'error' ? <Aviso tipo="error" texto={paso.mensaje} /> : null}
            <Boton
              texto={paso.tipo === 'error' ? 'Intentar de nuevo' : 'Iniciar sesión con el lector'}
              icono="nfc"
              cargando={paso.tipo === 'pidiendo'}
              alPresionar={comenzar}
            />
          </View>
        )}

        <Texto tipo="pequeno" style={{ textAlign: 'center' }}>
          API: {API_URL}
        </Texto>
      </View>
    </ScrollView>
  );
}

const estilos = StyleSheet.create({
  fondo: {
    flexGrow: 1,
    justifyContent: 'center',
    padding: Spacing.three,
  },
  tarjeta: {
    width: '100%',
    maxWidth: 420,
    alignSelf: 'center',
    borderRadius: 12,
    padding: Spacing.four,
    gap: Spacing.three,
  },
  marca: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
  },
  icono: {
    width: 36,
    height: 36,
    borderRadius: Radio,
    alignItems: 'center',
    justifyContent: 'center',
  },
  pictogramas: {
    flexDirection: 'row',
    gap: Spacing.four,
    marginTop: Spacing.two,
  },
  instruccion: {
    fontSize: 24,
    fontWeight: '700',
    textAlign: 'center',
    lineHeight: 30,
  },
  cuenta: {
    fontSize: 56,
    fontWeight: '700',
    fontVariant: ['tabular-nums'],
  },
});
