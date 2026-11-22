
import { CarruselCultivos } from '@/components/inicio/CarruselCultivos';
import { DiagnosticoCard } from '@/components/inicio/DiagnosticoCard';
import { FilaAccion } from '@/components/inicio/FilaAccion';
import { ResumenDeposito } from '@/components/inicio/ResumenDeposito';
import { TarjetaMetrica } from '@/components/inicio/TarjetaMetrica';
import {
  LUZ,
  NIVEL_DEPOSITO,
  PLANTAS,
  TEMPERATURA,
} from '@/components/inicio/datos';

import { ErrorConReintento } from '@/components/ui/Estados';
import { useConfiguracion } from '@/context/ConfiguracionContext';
import { useTema } from '@/hooks/useTema';
import { colores } from '@/theme/tokens';

import { useQuery } from '@tanstack/react-query';
import { useFocusEffect } from 'expo-router';

import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';

import {
  Pressable,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  View,
} from 'react-native';

import { backendApiEnabled } from '../../../services/backendApi';

import {
  obtenerLecturasBackend,
  obtenerRiegosBackend,
} from '../../../services/telemetriaService';

import {
  formatearFecha,
  formatearHora,
} from '../../../utils/formatoFechaHora';

const INTERVALO_REFRESCO_MS = 10_000;

function conUnidad(
  valor: number | undefined,
  unidad: string,
): string {
  return valor === undefined ? '—' : `${valor}${unidad}`;
}

export default function Inicio() {
  const tema = useTema();
  const { config } = useConfiguracion();

  const s = useMemo(
    () => crearEstilos(tema),
    [tema],
  );

  const [plantaId, setPlantaId] = useState(PLANTAS[0].id);
  const [bomba, setBomba] = useState(false);
  const [ventilador, setVentilador] = useState(false);
  const [leds, setLeds] = useState(true);

  const temporizador = useRef<ReturnType<typeof setTimeout> | null>(
    null,
  );

  const planta =
    PLANTAS.find((p) => p.id === plantaId) ?? PLANTAS[0];

  // Las pestañas permanecen montadas:
  // el sondeo solo corre mientras esta pantalla está visible.
  const [enfocada, setEnfocada] = useState(true);

  useFocusEffect(
    useCallback(() => {
      setEnfocada(true);

      return () => setEnfocada(false);
    }, []),
  );

  const intervalo =
    backendApiEnabled && enfocada
      ? INTERVALO_REFRESCO_MS
      : false;

  const lecturasBackend = useQuery({
    queryKey: ['backend', 'lecturas'],
    queryFn: obtenerLecturasBackend,
    enabled: backendApiEnabled,
    refetchInterval: intervalo,
  });

  const riegosBackend = useQuery({
    queryKey: ['backend', 'riegos'],
    queryFn: obtenerRiegosBackend,
    enabled: backendApiEnabled,
    refetchInterval: intervalo,
  });

  useEffect(
    () => () => {
      if (temporizador.current) {
        clearTimeout(temporizador.current);
      }
    },
    [],
  );

  const estadoApi =
    lecturasBackend.isError || riegosBackend.isError
      ? 'API desconectada'
      : lecturasBackend.isSuccess && riegosBackend.isSuccess
        ? 'API conectada'
        : 'Conectando con API…';

  // Valores a mostrar.
  const lecturas = lecturasBackend.data;
  const ultimoRiego = riegosBackend.data?.[0];

  const valorHumedad = backendApiEnabled
    ? conUnidad(lecturas?.humedadSuelo, '%')
    : `${planta.humedad}%`;

  const valorTemperatura = backendApiEnabled
    ? conUnidad(lecturas?.temperatura, ' °C')
    : `${TEMPERATURA} °C`;

  const luzMostrada = backendApiEnabled
    ? lecturas?.luz
    : LUZ;

  const valorLuz =
    luzMostrada === undefined
      ? '— lx'
      : `${luzMostrada.toLocaleString('es-MX')} lx`;

  const nivelDeposito = backendApiEnabled
    ? (lecturas?.nivelDeposito ?? 0)
    : NIVEL_DEPOSITO;

  const ultimoRiegoMl = backendApiEnabled
    ? (ultimoRiego?.volumenMl ?? 0)
    : planta.riegoSugeridoMl;

  function regarEnSimulador() {
    if (bomba) return;

    setBomba(true);

    temporizador.current = setTimeout(
      () => setBomba(false),
      3000,
    );
  }

  const colorSwitch = {
    false: tema.surfaceContainerHighest,
    true: tema.primary,
  };

  if (
    backendApiEnabled &&
    (lecturasBackend.isLoading || riegosBackend.isLoading)
  ) {
    return (
      <View style={s.pantalla}>
        <View style={s.mensaje}>
          <Text style={s.titulo}>
            Conectando con el Backend…
          </Text>
        </View>
      </View>
    );
  }

  if (
    backendApiEnabled &&
    (lecturasBackend.isError || riegosBackend.isError)
  ) {
    const error =
      lecturasBackend.error ?? riegosBackend.error;

    return (
      <View style={s.pantalla}>
        <View style={s.mensaje}>
          <ErrorConReintento
            mensaje={
              error instanceof Error
                ? error.message
                : 'No se pudo consultar la API de Telemetría.'
            }
            intentarDeNuevo={() => {
              void lecturasBackend.refetch();
              void riegosBackend.refetch();
            }}
          />
        </View>
      </View>
    );
  }

  return (
    <View style={s.pantalla}>
      <ScrollView
        contentContainerStyle={s.contenido}
        showsVerticalScrollIndicator={false}
      >
        <CarruselCultivos
          plantas={PLANTAS}
          seleccionada={plantaId}
          onSelect={setPlantaId}
        />

        <DiagnosticoCard
          vigorPct={planta.vigorPct}
          descripcion={planta.descripcion}
        />

        <Text style={s.titulo}>
          Telemetría y Accionadores
        </Text>

        {backendApiEnabled && (
          <Text style={s.aviso}>
            {estadoApi} · Información de solo lectura recibida
            desde el sistema embebido.
          </Text>
        )}

        <TarjetaMetrica
          icono="water-drop"
          colorIcono={tema.onSecondaryContainer}
          fondoIcono={tema.secondaryContainer}
          etiqueta="Humedad Sustrato"
          valor={valorHumedad}
          detalle={`Óptimo (${planta.humedadOptima[0]}-${planta.humedadOptima[1]}%)`}
        >
          {backendApiEnabled ? (
            <FilaAccion
              titulo="Último riego"
              subtitulo={
                ultimoRiego
                  ? `${ultimoRiego.volumenMl} mL · ${formatearFecha(
                      ultimoRiego.inicio,
                      config.formatoFecha,
                    )} ${formatearHora(
                      ultimoRiego.inicio,
                      config.formatoHora,
                    )}`
                  : 'Sin riegos registrados'
              }
            />
          ) : (
            <FilaAccion
              titulo="Bomba Sumergible"
              subtitulo={
                bomba
                  ? 'Dosificando…'
                  : 'En reposo · Pulso manual'
              }
            >
              <Pressable
                onPress={regarEnSimulador}
                disabled={bomba}
                style={[
                  s.boton,
                  bomba && { opacity: 0.5 },
                ]}
              >
                <Text style={s.botonTexto}>
                  {bomba
                    ? 'Regando…'
                    : `Regar ${planta.riegoSugeridoMl} mL`}
                </Text>
              </Pressable>
            </FilaAccion>
          )}
        </TarjetaMetrica>

        <TarjetaMetrica
          icono="device-thermostat"
          colorIcono={tema.onTertiaryContainer}
          fondoIcono={tema.tertiaryContainer}
          etiqueta="Temperatura Ambiente"
          valor={valorTemperatura}
        >
          {!backendApiEnabled && (
            <FilaAccion
              titulo="Ventilador Extractor"
              subtitulo={ventilador ? 'En marcha' : 'Apagado'}
            >
              <Switch
                value={ventilador}
                onValueChange={setVentilador}
                trackColor={colorSwitch}
              />
            </FilaAccion>
          )}
        </TarjetaMetrica>

        <TarjetaMetrica
          icono="wb-sunny"
          colorIcono={tema.onPrimaryContainer}
          fondoIcono={tema.primaryContainer}
          etiqueta="Radiación Lumínica"
          valor={valorLuz}
        >
          {!backendApiEnabled && (
            <FilaAccion
              titulo="Luminaria Espectro Completo"
              subtitulo={leds ? 'Encendida' : 'Apagada'}
            >
              <Switch
                value={leds}
                onValueChange={setLeds}
                trackColor={colorSwitch}
              />
            </FilaAccion>
          )}
        </TarjetaMetrica>

        <ResumenDeposito
          nivelDeposito={nivelDeposito}
          ultimoRiegoMl={ultimoRiegoMl}
        />
      </ScrollView>
    </View>
  );
}

const crearEstilos = (
  tema: (typeof colores)[keyof typeof colores],
) =>
  StyleSheet.create({
    pantalla: {
      flex: 1,
      backgroundColor: tema.surface,
    },
    contenido: {
      padding: 16,
      paddingBottom: 32,
      gap: 12,
    },
    mensaje: {
      padding: 16,
    },
    titulo: {
      fontSize: 16,
      fontWeight: '600',
      color: tema.onSurface,
      marginTop: 4,
    },
    aviso: {
      fontSize: 12,
      color: tema.onSurfaceVariant,
    },
    boton: {
      borderRadius: 999,
      paddingHorizontal: 12,
      paddingVertical: 8,
      backgroundColor: tema.secondary,
    },
    botonTexto: {
      fontSize: 11,
      fontWeight: '500',
      color: tema.onSecondary,
    },
  });
