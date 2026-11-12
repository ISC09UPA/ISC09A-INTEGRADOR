import { useEffect, useRef, useState } from 'react';
import { Pressable, ScrollView, StyleSheet, Switch, Text, View } from 'react-native';
import { useQuery } from '@tanstack/react-query';
import { MaterialIcons } from '@expo/vector-icons';
import { CarruselCultivos } from '@/components/inicio/CarruselCultivos';
import { DiagnosticoCard } from '@/components/inicio/DiagnosticoCard';
import { FilaAccion } from '@/components/inicio/FilaAccion';
import { HeaderInicio } from '@/components/inicio/HeaderInicio';
import { ResumenDeposito } from '@/components/inicio/ResumenDeposito';
import { TarjetaMetrica } from '@/components/inicio/TarjetaMetrica';
import { LUZ, NIVEL_DEPOSITO, PLANTAS, TEMPERATURA } from '@/components/inicio/datos';
import { ErrorConReintento } from '@/components/ui/Estados';
import { backendApiEnabled } from '../../../services/backendApi';
import { obtenerLecturasBackend, obtenerRiegosBackend } from '../../../services/telemetriaService';
import { colores } from '@/theme/tokens';

export default function Inicio() {
  const [plantaId, setPlantaId] = useState(PLANTAS[0].id);
  const [bomba, setBomba] = useState(false);
  const [ventilador, setVentilador] = useState(false);
  const [leds, setLeds] = useState(true);
  const temporizador = useRef<ReturnType<typeof setTimeout> | null>(null);
  const planta = PLANTAS.find((p) => p.id === plantaId) ?? PLANTAS[0];
  const lecturasBackend = useQuery({
    queryKey: ['backend', 'lecturas'],
    queryFn: obtenerLecturasBackend,
    enabled: backendApiEnabled,
    refetchInterval: backendApiEnabled ? 10_000 : false,
  });
  const riegosBackend = useQuery({
    queryKey: ['backend', 'riegos'],
    queryFn: obtenerRiegosBackend,
    enabled: backendApiEnabled,
    refetchInterval: backendApiEnabled ? 10_000 : false,
  });
  const estadoApi = lecturasBackend.isError || riegosBackend.isError
    ? 'API desconectada'
    : lecturasBackend.isSuccess && riegosBackend.isSuccess
      ? 'API conectada'
      : 'Conectando con API…';
  const encabezado = backendApiEnabled ? (
    <View style={s.encabezado}>
      <View style={s.filaEncabezado}>
        <MaterialIcons name="eco" size={22} color={colores.primary} />
        <View>
          <Text style={s.headerTitulo}>RiegoSmart IA</Text>
          <Text style={s.headerSub}>{estadoApi}</Text>
        </View>
      </View>
    </View>
  ) : (
    <HeaderInicio />
  );

  useEffect(() => () => {
    if (temporizador.current) clearTimeout(temporizador.current);
  }, []);

  const lecturas = lecturasBackend.data;
  const humedadSuelo = lecturas?.humedadSuelo;
  const temperatura = lecturas?.temperatura;
  const luz = lecturas?.luz;
  const nivelDeposito = lecturas?.nivelDeposito;
  const ultimoRiego = riegosBackend.data?.[0];
  const valorLuz = backendApiEnabled
    ? luz === undefined
      ? '— lx'
      : `${luz.toLocaleString('es-MX')} lx`
    : `${LUZ.toLocaleString('es-MX')} lx`;

  function regarEnSimulador() {
    if (bomba) return;
    setBomba(true);
    temporizador.current = setTimeout(() => setBomba(false), 3000);
  }

  const colorSwitch = { false: colores.surfaceContainerHighest, true: colores.primary };

  if (backendApiEnabled && (lecturasBackend.isLoading || riegosBackend.isLoading)) {
    return (
      <View style={s.pantalla}>
        {encabezado}
        <View style={s.mensaje}>
          <Text style={s.titulo}>Conectando con el Backend…</Text>
        </View>
      </View>
    );
  }

  if (backendApiEnabled && (lecturasBackend.isError || riegosBackend.isError)) {
    const error = lecturasBackend.error ?? riegosBackend.error;
    return (
      <View style={s.pantalla}>
        {encabezado}
        <View style={s.mensaje}>
          <ErrorConReintento
            mensaje={error instanceof Error ? error.message : 'No se pudo consultar la API de Telemetría.'}
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
      {encabezado}
      <ScrollView contentContainerStyle={s.contenido} showsVerticalScrollIndicator={false}>
        <CarruselCultivos plantas={PLANTAS} seleccionada={plantaId} onSelect={setPlantaId} />
        <DiagnosticoCard vigorPct={planta.vigorPct} descripcion={planta.descripcion} />

        <Text style={s.titulo}>Telemetría y Accionadores</Text>
        {backendApiEnabled && (
          <Text style={s.aviso}>
            Información de solo lectura recibida desde el sistema embebido.
          </Text>
        )}

        <TarjetaMetrica
          icono="water-drop"
          colorIcono={colores.onSecondaryContainer}
          fondoIcono={colores.secondaryContainer}
          etiqueta="Humedad Sustrato"
          valor={`${backendApiEnabled ? humedadSuelo ?? '—' : planta.humedad}%`}
          detalle={`Óptimo (${planta.humedadOptima[0]}-${planta.humedadOptima[1]}%)`}
        >
          {backendApiEnabled ? (
            <FilaAccion
              titulo="Último riego"
              subtitulo={
                ultimoRiego
                  ? `${ultimoRiego.volumenMl} mL · ${new Date(ultimoRiego.inicio).toLocaleString('es-MX')}`
                  : 'Sin riegos registrados'
              }
            />
          ) : (
            <FilaAccion
              titulo="Bomba Sumergible"
              subtitulo={bomba ? 'Dosificando…' : 'En reposo · Pulso manual'}
            >
              <Pressable
                onPress={regarEnSimulador}
                disabled={bomba}
                style={[s.boton, bomba && { opacity: 0.5 }]}
              >
                <Text style={s.botonTexto}>{bomba ? 'Regando…' : `Regar ${planta.riegoSugeridoMl} mL`}</Text>
              </Pressable>
            </FilaAccion>
          )}
        </TarjetaMetrica>

        <TarjetaMetrica
          icono="device-thermostat"
          colorIcono={colores.onTertiaryContainer}
          fondoIcono={colores.tertiaryContainer}
          etiqueta="Temperatura Ambiente"
          valor={`${backendApiEnabled ? temperatura ?? '—' : TEMPERATURA} °C`}
        >
          {!backendApiEnabled && (
            <FilaAccion titulo="Ventilador Extractor" subtitulo={ventilador ? 'En marcha' : 'Apagado'}>
              <Switch value={ventilador} onValueChange={setVentilador} trackColor={colorSwitch} />
            </FilaAccion>
          )}
        </TarjetaMetrica>

        <TarjetaMetrica
          icono="wb-sunny"
          colorIcono={colores.onPrimaryContainer}
          fondoIcono={colores.primaryContainer}
          etiqueta="Radiación Lumínica"
          valor={valorLuz}
        >
          {!backendApiEnabled && (
            <FilaAccion titulo="Luminaria Espectro Completo" subtitulo={leds ? 'Encendida' : 'Apagada'}>
              <Switch value={leds} onValueChange={setLeds} trackColor={colorSwitch} />
            </FilaAccion>
          )}
        </TarjetaMetrica>

        <ResumenDeposito
          nivelDeposito={backendApiEnabled ? nivelDeposito ?? 0 : NIVEL_DEPOSITO}
          ultimoRiegoMl={
            backendApiEnabled
              ? ultimoRiego?.volumenMl ?? 0
              : planta.riegoSugeridoMl
          }
        />
      </ScrollView>
    </View>
  );
}

const s = StyleSheet.create({
  encabezado: {
    height: 64,
    marginTop: 40,
    paddingHorizontal: 16,
    flexDirection: 'row',
    alignItems: 'center',
  },
  filaEncabezado: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  headerTitulo: { fontSize: 16, fontWeight: '600', color: colores.onSurface },
  headerSub: { fontSize: 12, color: colores.onSurfaceVariant },
  pantalla: { flex: 1, backgroundColor: colores.surface },
  contenido: { padding: 16, paddingBottom: 32, gap: 12 },
  mensaje: { padding: 16 },
  titulo: { fontSize: 16, fontWeight: '600', color: colores.onSurface, marginTop: 4 },
  aviso: { fontSize: 12, color: colores.onSurfaceVariant },
  boton: { borderRadius: 999, paddingHorizontal: 12, paddingVertical: 8, backgroundColor: colores.secondary },
  botonTexto: { fontSize: 11, fontWeight: '500', color: colores.onSecondary },
});
