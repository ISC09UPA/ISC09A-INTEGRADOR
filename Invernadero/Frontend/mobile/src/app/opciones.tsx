
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
import { useColorScheme } from 'nativewind';

import { colores } from '@/theme/tokens';

import {
  CONFIGURACION_INICIAL,
  guardarConfiguracion,
  obtenerConfiguracion,
  type ConfiguracionApp,
} from '../../services/configuracionService';

import {
  ACERCA_DE,
  POLITICAS,
} from '@/constants/contenidoOpciones';

type TemaApp = 'light' | 'dark' | 'system';

type ModalTipo =
  | 'tema'
  | 'fecha'
  | 'hora'
  | 'acerca'
  | 'politicas'
  | null;

const CLAVE_TEMA = '@riegoSmart/tema';

export default function Opciones() {
  const { colorScheme, setColorScheme } = useColorScheme();

  const tema =
    colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const [config, setConfig] = useState<ConfiguracionApp>(
    CONFIGURACION_INICIAL,
  );

  const [temaSeleccionado, setTemaSeleccionado] =
    useState<TemaApp>('system');

  const [modal, setModal] = useState<ModalTipo>(null);

  // Cargar preferencias guardadas
  useEffect(() => {
    async function cargarPreferencias() {
      try {
        const configuracion = await obtenerConfiguracion();
        setConfig(configuracion);

        const temaGuardado = await AsyncStorage.getItem(CLAVE_TEMA);

        if (
          temaGuardado === 'light' ||
          temaGuardado === 'dark' ||
          temaGuardado === 'system'
        ) {
          setTemaSeleccionado(temaGuardado);
          setColorScheme(temaGuardado);
        }
      } catch {
        Alert.alert(
          'Error',
          'No se pudieron cargar las preferencias.',
        );
      }
    }

    cargarPreferencias();
  }, [setColorScheme]);

  // Actualizar preferencias generales
  async function actualizar<K extends keyof ConfiguracionApp>(
    propiedad: K,
    valor: ConfiguracionApp[K],
  ) {
    const anterior = config;

    const nuevaConfiguracion = {
      ...config,
      [propiedad]: valor,
    };

    setConfig(nuevaConfiguracion);

    try {
      await guardarConfiguracion(nuevaConfiguracion);
    } catch {
      setConfig(anterior);

      Alert.alert(
        'Error',
        'No se pudo guardar la configuración.',
      );
    }
  }

  // Cambiar tema
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

  // Abrir configuración del dispositivo
  function abrirConfiguracionDispositivo() {
    Linking.openSettings().catch(() => {
      Alert.alert(
        'Error',
        'No se pudo abrir la configuración del dispositivo.',
      );
    });
  }

  // Fecha de ejemplo
  const fechaActual = new Date();

  const dia = String(fechaActual.getDate()).padStart(2, '0');
  const mes = String(fechaActual.getMonth() + 1).padStart(2, '0');
  const anio = fechaActual.getFullYear();

  const fechaEjemplo =
    config.formatoFecha === 'MM/dd/yyyy'
      ? `${mes}/${dia}/${anio}`
      : config.formatoFecha === 'yyyy-MM-dd'
        ? `${anio}-${mes}-${dia}`
        : `${dia}/${mes}/${anio}`;

  // Hora de ejemplo
  const horaEjemplo = fechaActual.toLocaleTimeString('es-MX', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: config.formatoHora === '12',
  });

  // Componente de opción
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

          <Text style={[s.descripcion, { color: tema.onSurfaceVariant }]}>
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

  // Componente de interruptor
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

          <Text style={[s.descripcion, { color: tema.onSurfaceVariant }]}>
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

  // Separador
  function Separador() {
    return (
      <View
        style={[
          s.separador,
          { backgroundColor: tema.surfaceContainerHigh },
        ]}
      />
    );
  }

  // Encabezado de sección
  function EncabezadoSeccion({
    children,
  }: {
    children: ReactNode;
  }) {
    return (
      <Text style={[s.seccion, { color: tema.onSurfaceVariant }]}>
        {children}
      </Text>
    );
  }

  return (
    <View style={[s.pantalla, { backgroundColor: tema.surface }]}>
      <ScrollView
        contentContainerStyle={s.contenido}
        showsVerticalScrollIndicator={false}
      >
        <Text style={[s.encabezado, { color: tema.onSurface }]}>
          Configuración
        </Text>

        <Text style={[s.subtitulo, { color: tema.onSurfaceVariant }]}>
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
              temaSeleccionado === 'light'
                ? 'Claro'
                : temaSeleccionado === 'dark'
                  ? 'Oscuro'
                  : 'Seguir dispositivo'
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
            descripcion={`${config.formatoHora === '24' ? '24 horas' : '12 horas'} · ${horaEjemplo}`}
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
            onChange={(v) => actualizar('notificaciones', v)}
          />

          <Separador />

          <Interruptor
            titulo="Sonido"
            descripcion="Reproducir sonido al recibir avisos."
            valor={config.sonido}
            onChange={(v) => actualizar('sonido', v)}
            deshabilitado={!config.notificaciones}
          />

          <Separador />

          <Interruptor
            titulo="Vibración"
            descripcion="Vibrar al recibir notificaciones."
            valor={config.vibracion}
            onChange={(v) => actualizar('vibracion', v)}
            deshabilitado={!config.notificaciones}
          />

          <Separador />

          <Interruptor
            titulo="Avisos dentro de la aplicación"
            descripcion="Mostrar alertas mientras utilizas la app."
            valor={config.avisosInternos}
            onChange={(v) => actualizar('avisosInternos', v)}
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

        <Text style={[s.version, { color: tema.onSurfaceVariant }]}>
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
              <Text style={[s.tituloModal, { color: tema.onSurface }]}>
                {modal === 'tema'
                  ? 'Tema de la aplicación'
                  : modal === 'fecha'
                    ? 'Formato de fecha'
                    : modal === 'hora'
                      ? 'Formato de hora'
                      : modal === 'acerca'
                        ? 'Acerca de'
                        : 'Políticas'}
              </Text>

              <Pressable onPress={() => setModal(null)}>
                <MaterialIcons
                  name="close"
                  size={24}
                  color={tema.onSurfaceVariant}
                />
              </Pressable>
            </View>

            {/* TEMA */}
            {modal === 'tema' && (
              <>
                {[
                  { valor: 'light', texto: 'Claro' },
                  { valor: 'dark', texto: 'Oscuro' },
                  { valor: 'system', texto: 'Seguir dispositivo' },
                ].map((opcion) => (
                  <Pressable
                    key={opcion.valor}
                    style={s.filaModal}
                    onPress={() => {
                      cambiarTema(opcion.valor as TemaApp);
                      setModal(null);
                    }}
                  >
                    <Text style={[s.textoModal, { color: tema.onSurface }]}>
                      {opcion.texto}
                    </Text>

                    {temaSeleccionado === opcion.valor && (
                      <MaterialIcons
                        name="check"
                        size={22}
                        color={tema.primary}
                      />
                    )}
                  </Pressable>
                ))}
              </>
            )}

            {/* FECHA */}
            {modal === 'fecha' && (
              <>
                {[
                  { valor: 'dd/MM/yyyy', texto: 'DD/MM/AAAA' },
                  { valor: 'MM/dd/yyyy', texto: 'MM/DD/AAAA' },
                  { valor: 'yyyy-MM-dd', texto: 'AAAA-MM-DD' },
                ].map((opcion) => (
                  <Pressable
                    key={opcion.valor}
                    style={s.filaModal}
                    onPress={() => {
                      actualizar(
                        'formatoFecha',
                        opcion.valor as ConfiguracionApp['formatoFecha'],
                      );
                      setModal(null);
                    }}
                  >
                    <Text style={[s.textoModal, { color: tema.onSurface }]}>
                      {opcion.texto}
                    </Text>

                    {config.formatoFecha === opcion.valor && (
                      <MaterialIcons
                        name="check"
                        size={22}
                        color={tema.primary}
                      />
                    )}
                  </Pressable>
                ))}
              </>
            )}

            {/* HORA */}
            {modal === 'hora' && (
              <>
                {[
                  { valor: '24', texto: '24 horas (14:30)' },
                  { valor: '12', texto: '12 horas (02:30 p. m.)' },
                ].map((opcion) => (
                  <Pressable
                    key={opcion.valor}
                    style={s.filaModal}
                    onPress={() => {
                      actualizar(
                        'formatoHora',
                        opcion.valor as ConfiguracionApp['formatoHora'],
                      );
                      setModal(null);
                    }}
                  >
                    <Text style={[s.textoModal, { color: tema.onSurface }]}>
                      {opcion.texto}
                    </Text>

                    {config.formatoHora === opcion.valor && (
                      <MaterialIcons
                        name="check"
                        size={22}
                        color={tema.primary}
                      />
                    )}
                  </Pressable>
                ))}
              </>
            )}

            {/* ACERCA DE */}
            {modal === 'acerca' && (
              <ScrollView style={{ maxHeight: 350 }}>
                <View style={s.textosInformativos}>
                  <Text style={[s.nombreApp, { color: tema.primary }]}>
                    {ACERCA_DE.nombre}
                  </Text>

                  <Text style={[s.textoModal, { color: tema.onSurface }]}>
                    {ACERCA_DE.descripcion}
                  </Text>

                  <Text style={[s.nombreApp, { color: tema.onSurface }]}>
                    Objetivo
                  </Text>

                  <Text style={[s.textoModal, { color: tema.onSurfaceVariant }]}>
                    {ACERCA_DE.objetivo}
                  </Text>

                  <Text style={[s.nombreApp, { color: tema.onSurface }]}>
                    Desarrolladores
                  </Text>

                  {ACERCA_DE.desarrolladores.map((persona, index) => (
                    <Text
                      key={index}
                      style={[s.textoModal, { color: tema.onSurfaceVariant }]}
                    >
                      • {persona}
                    </Text>
                  ))}

                  <Text style={[s.nombreApp, { color: tema.onSurface }]}>
                    Tecnologías
                  </Text>

                  {ACERCA_DE.tecnologias.map((tecnologia, index) => (
                    <Text
                      key={index}
                      style={[s.textoModal, { color: tema.onSurfaceVariant }]}
                    >
                      • {tecnologia}
                    </Text>
                  ))}

                  <Text style={[s.textoModal, { color: tema.onSurfaceVariant }]}>
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
                    <View key={politica.id} style={{ gap: 6 }}>
                      <Text style={[s.nombreApp, { color: tema.onSurface }]}>
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
              style={[s.botonCerrar, { backgroundColor: tema.primary }]}
              onPress={() => setModal(null)}
            >
              <Text style={{ color: tema.onPrimary, fontWeight: '600' }}>
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
