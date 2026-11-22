
import type { ReactNode } from 'react';
import { useEffect, useState } from 'react';

import {
  Alert,
  Linking,
  Modal,
  Pressable,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  View,
} from 'react-native';

import { MaterialIcons } from '@expo/vector-icons';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { router } from 'expo-router';
import { useColorScheme } from 'nativewind';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { ACERCA_DE, POLITICAS } from '@/constants/contenidoOpciones';
import { useConfiguracion } from '@/context/ConfiguracionContext';
import { useTema } from '@/hooks/useTema';

import {
  CLAVE_TEMA,
  type ConfiguracionApp,
} from '../../services/configuracionService';

import {
  formatearFecha,
  formatearHora,
} from '../../utils/formatoFechaHora';

type TemaApp = 'light' | 'dark' | 'system';

type ModalTipo =
  | 'tema'
  | 'fecha'
  | 'hora'
  | 'acerca'
  | 'politicas'
  | null;

const TITULOS_MODAL: Record<Exclude<ModalTipo, null>, string> = {
  tema: 'Tema de la aplicación',
  fecha: 'Formato de fecha',
  hora: 'Formato de hora',
  acerca: 'Acerca de',
  politicas: 'Políticas',
};

const OPCIONES_TEMA: {
  valor: TemaApp;
  texto: string;
}[] = [
  { valor: 'light', texto: 'Claro' },
  { valor: 'dark', texto: 'Oscuro' },
  { valor: 'system', texto: 'Seguir dispositivo' },
];

const OPCIONES_FECHA: {
  valor: ConfiguracionApp['formatoFecha'];
  texto: string;
}[] = [
  { valor: 'dd/MM/yyyy', texto: 'DD/MM/AAAA' },
  { valor: 'MM/dd/yyyy', texto: 'MM/DD/AAAA' },
  { valor: 'yyyy-MM-dd', texto: 'AAAA-MM-DD' },
];

const OPCIONES_HORA: {
  valor: ConfiguracionApp['formatoHora'];
  texto: string;
}[] = [
  { valor: '24', texto: '24 horas (14:30)' },
  { valor: '12', texto: '12 horas (02:30 p. m.)' },
];

// Componentes auxiliares.
function Seccion({
  titulo,
  descripcion,
  icono,
  onPress,
}: {
  titulo: string;
  descripcion: string;
  icono: keyof typeof MaterialIcons.glyphMap;
  onPress: () => void;
}) {
  const tema = useTema();

  return (
    <Pressable onPress={onPress} style={s.opcion}>
      <View
        style={[
          s.icono,
          { backgroundColor: tema.secondaryContainer },
        ]}
      >
        <MaterialIcons
          name={icono}
          size={22}
          color={tema.onSecondaryContainer}
        />
      </View>

      <View style={s.info}>
        <Text style={[s.tituloOpcion, { color: tema.onSurface }]}>
          {titulo}
        </Text>

        <Text
          style={[
            s.descripcion,
            { color: tema.onSurfaceVariant },
          ]}
        >
          {descripcion}
        </Text>
      </View>

      <MaterialIcons
        name="chevron-right"
        size={24}
        color={tema.onSurfaceVariant}
      />
    </Pressable>
  );
}

function Interruptor({
  titulo,
  descripcion,
  valor,
  onChange,
  deshabilitado = false,
}: {
  titulo: string;
  descripcion: string;
  valor: boolean;
  onChange: (valor: boolean) => void;
  deshabilitado?: boolean;
}) {
  const tema = useTema();

  return (
    <View
      style={[
        s.opcion,
        { opacity: deshabilitado ? 0.5 : 1 },
      ]}
    >
      <View style={s.info}>
        <Text style={[s.tituloOpcion, { color: tema.onSurface }]}>
          {titulo}
        </Text>

        <Text
          style={[
            s.descripcion,
            { color: tema.onSurfaceVariant },
          ]}
        >
          {descripcion}
        </Text>
      </View>

      <Switch
        value={valor}
        onValueChange={onChange}
        disabled={deshabilitado}
        trackColor={{
          false: tema.surfaceContainerHighest,
          true: tema.primary,
        }}
      />
    </View>
  );
}

function Separador() {
  const tema = useTema();

  return (
    <View
      style={[
        s.separador,
        { backgroundColor: tema.surfaceContainerHigh },
      ]}
    />
  );
}

function EncabezadoSeccion({
  children,
}: {
  children: ReactNode;
}) {
  const tema = useTema();

  return (
    <Text style={[s.seccion, { color: tema.onSurfaceVariant }]}>
      {children}
    </Text>
  );
}

export default function Opciones() {
  const { setColorScheme } = useColorScheme();

  const tema = useTema();
  const insets = useSafeAreaInsets();

  const {
    config,
    actualizar: actualizarConfiguracion,
  } = useConfiguracion();

  const [temaSeleccionado, setTemaSeleccionado] =
    useState<TemaApp>('system');

  const [modal, setModal] = useState<ModalTipo>(null);

  const [ahora] = useState(() => new Date());

  function volver() {
    if (router.canGoBack()) {
      router.back();
    } else {
      router.replace('/(tabs)/inicio');
    }
  }

  // Cargar el tema guardado.
  useEffect(() => {
    async function cargarPreferencias() {
      try {
        const temaGuardado = await AsyncStorage.getItem(CLAVE_TEMA);

        if (
          temaGuardado === 'light' ||
          temaGuardado === 'dark' ||
          temaGuardado === 'system'
        ) {
          setTemaSeleccionado(temaGuardado);
        }
      } catch {
        Alert.alert(
          'Error',
          'No se pudieron cargar las preferencias.',
        );
      }
    }

    void cargarPreferencias();
  }, []);

  // Actualizar preferencias compartidas.
  async function actualizar<K extends keyof ConfiguracionApp>(
    propiedad: K,
    valor: ConfiguracionApp[K],
  ) {
    const guardado = await actualizarConfiguracion(
      propiedad,
      valor,
    );

    if (!guardado) {
      Alert.alert(
        'Error',
        'No se pudo guardar la configuración.',
      );
    }
  }

  // Cambiar tema.
  async function cambiarTema(nuevoTema: TemaApp) {
    const anterior = temaSeleccionado;

    setTemaSeleccionado(nuevoTema);
    setColorScheme(nuevoTema);

    try {
      await AsyncStorage.setItem(CLAVE_TEMA, nuevoTema);
    } catch {
      setTemaSeleccionado(anterior);
      setColorScheme(anterior);

      Alert.alert(
        'Error',
        'No se pudo guardar el tema.',
      );
    }
  }

  function abrirConfiguracionDispositivo() {
    Linking.openSettings().catch(() => {
      Alert.alert(
        'Error',
        'No se pudo abrir la configuración del dispositivo.',
      );
    });
  }

  const fechaEjemplo = formatearFecha(
    ahora,
    config.formatoFecha,
  );

  const horaEjemplo = formatearHora(
    ahora,
    config.formatoHora,
  );

  // Filas del modal de selección.
  const seleccion =
    modal === 'tema'
      ? {
          opciones: OPCIONES_TEMA as {
            valor: string;
            texto: string;
          }[],
          actual: temaSeleccionado as string,
          elegir: (v: string) =>
            void cambiarTema(v as TemaApp),
        }
      : modal === 'fecha'
        ? {
            opciones: OPCIONES_FECHA as {
              valor: string;
              texto: string;
            }[],
            actual: config.formatoFecha as string,
            elegir: (v: string) =>
              void actualizar(
                'formatoFecha',
                v as ConfiguracionApp['formatoFecha'],
              ),
          }
        : modal === 'hora'
          ? {
              opciones: OPCIONES_HORA as {
                valor: string;
                texto: string;
              }[],
              actual: config.formatoHora as string,
              elegir: (v: string) =>
                void actualizar(
                  'formatoHora',
                  v as ConfiguracionApp['formatoHora'],
                ),
            }
          : null;

  return (
    <View
      style={[
        s.pantalla,
        {
          backgroundColor: tema.surface,
          paddingTop: insets.top,
        },
      ]}
    >
      <ScrollView
        contentContainerStyle={s.contenido}
        showsVerticalScrollIndicator={false}
      >
        {/* ENCABEZADO */}
        <View style={s.encabezadoSuperior}>
          <Pressable
            onPress={volver}
            style={s.botonVolver}
            accessibilityRole="button"
            accessibilityLabel="Volver"
          >
            <MaterialIcons
              name="arrow-back"
              size={22}
              color={tema.onSurface}
            />
          </Pressable>

          <Text style={[s.encabezado, { color: tema.onSurface }]}>
            Configuración
          </Text>
        </View>

        <Text
          style={[
            s.subtitulo,
            { color: tema.onSurfaceVariant },
          ]}
        >
          Personaliza el funcionamiento de la aplicación.
        </Text>

        {/* APARIENCIA */}
        <EncabezadoSeccion>APARIENCIA</EncabezadoSeccion>

        <View
          style={[
            s.grupo,
            { backgroundColor: tema.surfaceContainerLowest },
          ]}
        >
          <Seccion
            titulo="Tema de la aplicación"
            descripcion={
              OPCIONES_TEMA.find(
                (o) => o.valor === temaSeleccionado,
              )?.texto ?? 'Seguir dispositivo'
            }
            icono="palette"
            onPress={() => setModal('tema')}
          />
        </View>

        {/* REGIÓN Y FORMATO */}
        <EncabezadoSeccion>REGIÓN Y FORMATO</EncabezadoSeccion>

        <View
          style={[
            s.grupo,
            { backgroundColor: tema.surfaceContainerLowest },
          ]}
        >
          <Seccion
            titulo="Formato de fecha"
            descripcion={`Ejemplo: ${fechaEjemplo}`}
            icono="calendar-today"
            onPress={() => setModal('fecha')}
          />

          <Separador />

          <Seccion
            titulo="Formato de hora"
            descripcion={`${
              config.formatoHora === '24'
                ? '24 horas'
                : '12 horas'
            } · ${horaEjemplo}`}
            icono="schedule"
            onPress={() => setModal('hora')}
          />
        </View>

        {/* NOTIFICACIONES */}
        <EncabezadoSeccion>NOTIFICACIONES</EncabezadoSeccion>

        <View
          style={[
            s.grupo,
            { backgroundColor: tema.surfaceContainerLowest },
          ]}
        >
          <Interruptor
            titulo="Notificaciones"
            descripcion="Activar o desactivar las preferencias de avisos."
            valor={config.notificaciones}
            onChange={(v) =>
              void actualizar('notificaciones', v)
            }
          />

          <Separador />

          <Interruptor
            titulo="Sonido"
            descripcion="Reproducir sonido al recibir avisos."
            valor={config.sonido}
            onChange={(v) => void actualizar('sonido', v)}
            deshabilitado={!config.notificaciones}
          />

          <Separador />

          <Interruptor
            titulo="Vibración"
            descripcion="Respuesta háptica al tocar botones y controles."
            valor={config.vibracion}
            onChange={(v) => void actualizar('vibracion', v)}
            deshabilitado={!config.notificaciones}
          />

          <Separador />

          <Interruptor
            titulo="Avisos dentro de la aplicación"
            descripcion="Mostrar alertas mientras utilizas la app."
            valor={config.avisosInternos}
            onChange={(v) =>
              void actualizar('avisosInternos', v)
            }
            deshabilitado={!config.notificaciones}
          />
        </View>

        {/* INFORMACIÓN */}
        <EncabezadoSeccion>INFORMACIÓN</EncabezadoSeccion>

        <View
          style={[
            s.grupo,
            { backgroundColor: tema.surfaceContainerLowest },
          ]}
        >
          <Seccion
            titulo="Acerca de"
            descripcion="Información y detalles de RiegoSmart IA."
            icono="info-outline"
            onPress={() => setModal('acerca')}
          />

          <Separador />

          <Seccion
            titulo="Políticas"
            descripcion="Privacidad, términos y condiciones."
            icono="description"
            onPress={() => setModal('politicas')}
          />

          <Separador />

          <Seccion
            titulo="Privacidad y permisos"
            descripcion="Consultar los permisos del dispositivo."
            icono="security"
            onPress={abrirConfiguracionDispositivo}
          />
        </View>

        <Text
          style={[
            s.version,
            { color: tema.onSurfaceVariant },
          ]}
        >
          {ACERCA_DE.nombre} · Versión {ACERCA_DE.version}
        </Text>
      </ScrollView>

      {/* MODALES */}
      <Modal
        visible={modal !== null}
        transparent
        animationType="fade"
        onRequestClose={() => setModal(null)}
      >
        <View style={s.fondoModal}>
          <View
            style={[
              s.contenidoModal,
              { backgroundColor: tema.surfaceContainerLowest },
            ]}
          >
            {/* TÍTULO */}
            <View style={s.encabezadoModal}>
              <Text
                style={[
                  s.tituloModal,
                  { color: tema.onSurface },
                ]}
              >
                {modal ? TITULOS_MODAL[modal] : ''}
              </Text>

              <Pressable
                onPress={() => setModal(null)}
                accessibilityRole="button"
                accessibilityLabel="Cerrar"
              >
                <MaterialIcons
                  name="close"
                  size={24}
                  color={tema.onSurfaceVariant}
                />
              </Pressable>
            </View>

            {/* TEMA / FECHA / HORA */}
            {seleccion?.opciones.map((opcion) => (
              <Pressable
                key={opcion.valor}
                style={s.filaModal}
                onPress={() => {
                  seleccion.elegir(opcion.valor);
                  setModal(null);
                }}
              >
                <Text
                  style={[
                    s.textoModal,
                    { color: tema.onSurface },
                  ]}
                >
                  {opcion.texto}
                </Text>

                {seleccion.actual === opcion.valor && (
                  <MaterialIcons
                    name="check"
                    size={22}
                    color={tema.primary}
                  />
                )}
              </Pressable>
            ))}

            {/* ACERCA DE */}
            {modal === 'acerca' && (
              <ScrollView style={{ maxHeight: 350 }}>
                <View style={s.textosInformativos}>
                  <Text
                    style={[
                      s.nombreApp,
                      { color: tema.primary },
                    ]}
                  >
                    {ACERCA_DE.nombre}
                  </Text>

                  <Text
                    style={[
                      s.textoModal,
                      { color: tema.onSurface },
                    ]}
                  >
                    {ACERCA_DE.descripcion}
                  </Text>

                  <Text
                    style={[
                      s.nombreApp,
                      { color: tema.onSurface },
                    ]}
                  >
                    Objetivo
                  </Text>

                  <Text
                    style={[
                      s.textoModal,
                      { color: tema.onSurfaceVariant },
                    ]}
                  >
                    {ACERCA_DE.objetivo}
                  </Text>

                  <Text
                    style={[
                      s.nombreApp,
                      { color: tema.onSurface },
                    ]}
                  >
                    Desarrolladores
                  </Text>

                  {ACERCA_DE.desarrolladores.map((persona) => (
                    <Text
                      key={persona}
                      style={[
                        s.textoModal,
                        { color: tema.onSurfaceVariant },
                      ]}
                    >
                      • {persona}
                    </Text>
                  ))}

                  <Text
                    style={[
                      s.nombreApp,
                      { color: tema.onSurface },
                    ]}
                  >
                    Tecnologías
                  </Text>

                  {ACERCA_DE.tecnologias.map((tecnologia) => (
                    <Text
                      key={tecnologia}
                      style={[
                        s.textoModal,
                        { color: tema.onSurfaceVariant },
                      ]}
                    >
                      • {tecnologia}
                    </Text>
                  ))}

                  <Text
                    style={[
                      s.textoModal,
                      { color: tema.onSurfaceVariant },
                    ]}
                  >
                    Versión {ACERCA_DE.version}
                  </Text>
                </View>
              </ScrollView>
            )}

            {/* POLÍTICAS */}
            {modal === 'politicas' && (
              <ScrollView style={{ maxHeight: 350 }}>
                <View style={s.textosInformativos}>
                  {POLITICAS.map((politica) => (
                    <View
                      key={politica.id}
                      style={{ gap: 6 }}
                    >
                      <Text
                        style={[
                          s.nombreApp,
                          { color: tema.onSurface },
                        ]}
                      >
                        {politica.titulo}
                      </Text>

                      <Text
                        style={[
                          s.textoModal,
                          { color: tema.onSurfaceVariant },
                        ]}
                      >
                        {politica.contenido}
                      </Text>
                    </View>
                  ))}
                </View>
              </ScrollView>
            )}

            {/* CERRAR */}
            <Pressable
              style={[
                s.botonCerrar,
                { backgroundColor: tema.primary },
              ]}
              onPress={() => setModal(null)}
            >
              <Text
                style={{
                  color: tema.onPrimary,
                  fontWeight: '600',
                }}
              >
                Cerrar
              </Text>
            </Pressable>
          </View>
        </View>
      </Modal>
    </View>
  );
}

const s = StyleSheet.create({
  pantalla: {
    flex: 1,
  },
  contenido: {
    padding: 16,
    paddingBottom: 36,
    gap: 12,
  },
  encabezadoSuperior: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  botonVolver: {
    width: 40,
    height: 40,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 999,
    backgroundColor: 'rgba(128,128,128,0.12)',
  },
  encabezado: {
    fontSize: 24,
    fontWeight: '700',
  },
  subtitulo: {
    fontSize: 14,
    marginTop: -8,
    marginBottom: 8,
  },
  seccion: {
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 1,
    marginTop: 12,
    marginBottom: 2,
  },
  grupo: {
    borderRadius: 16,
    overflow: 'hidden',
  },
  opcion: {
    minHeight: 70,
    paddingHorizontal: 14,
    paddingVertical: 12,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  icono: {
    width: 40,
    height: 40,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
  },
  info: {
    flex: 1,
    gap: 4,
  },
  tituloOpcion: {
    fontSize: 14,
    fontWeight: '600',
  },
  descripcion: {
    fontSize: 12,
  },
  separador: {
    height: StyleSheet.hairlineWidth,
    marginLeft: 66,
  },
  version: {
    fontSize: 12,
    textAlign: 'center',
    marginTop: 12,
  },
  fondoModal: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    padding: 24,
  },
  contenidoModal: {
    borderRadius: 20,
    padding: 20,
    gap: 12,
  },
  encabezadoModal: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 8,
  },
  tituloModal: {
    fontSize: 18,
    fontWeight: '700',
  },
  filaModal: {
    minHeight: 48,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
  },
  textoModal: {
    fontSize: 14,
    lineHeight: 21,
  },
  textosInformativos: {
    gap: 12,
  },
  nombreApp: {
    fontSize: 15,
    fontWeight: '700',
  },
  botonCerrar: {
    alignItems: 'center',
    justifyContent: 'center',
    padding: 13,
    borderRadius: 24,
    marginTop: 8,
  },
});
