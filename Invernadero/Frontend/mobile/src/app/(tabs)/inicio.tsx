
import { useEffect, useRef, useState } from 'react';
import {
  Pressable,
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  View,
} from 'react-native';
import { useColorScheme } from 'nativewind';

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
import { colores } from '@/theme/tokens';

export default function Inicio() {
  const { colorScheme } = useColorScheme();

  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const [plantaId, setPlantaId] = useState(PLANTAS[0].id);
  const [bomba, setBomba] = useState(false);
  const [ventilador, setVentilador] = useState(false);
  const [leds, setLeds] = useState(true);

  const temporizador = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (temporizador.current) clearTimeout(temporizador.current);
    };
  }, []);

  const planta = PLANTAS.find((p) => p.id === plantaId) ?? PLANTAS[0];

  function regar() {
    if (bomba) return;

    setBomba(true);
    temporizador.current = setTimeout(() => setBomba(false), 3000);
  }

  const colorSwitch = {
    false: tema.surfaceContainerHighest,
    true: tema.primary,
  };

  const s = StyleSheet.create({
    pantalla: {
      flex: 1,
      backgroundColor: tema.surface,
    },
    contenido: {
      padding: 16,
      paddingBottom: 32,
      gap: 12,
    },
    titulo: {
      fontSize: 16,
      fontWeight: '600',
      color: tema.onSurface,
      marginTop: 4,
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

        <Text style={s.titulo}>Telemetría y Accionadores</Text>

        <TarjetaMetrica
          icono="water-drop"
          colorIcono={tema.onSecondaryContainer}
          fondoIcono={tema.secondaryContainer}
          etiqueta="Humedad Sustrato"
          valor={`${planta.humedad}%`}
          detalle={`Óptimo (${planta.humedadOptima[0]}-${planta.humedadOptima[1]}%)`}
        >
          <FilaAccion
            titulo="Bomba Sumergible"
            subtitulo={bomba ? 'Dosificando…' : 'En reposo · Pulso manual'}
          >
            <Pressable
              onPress={regar}
              disabled={bomba}
              style={[s.boton, bomba && { opacity: 0.5 }]}
            >
              <Text style={s.botonTexto}>
                {bomba ? 'Regando…' : `Regar ${planta.riegoSugeridoMl} mL`}
              </Text>
            </Pressable>
          </FilaAccion>
        </TarjetaMetrica>

        <TarjetaMetrica
          icono="device-thermostat"
          colorIcono={tema.onTertiaryContainer}
          fondoIcono={tema.tertiaryContainer}
          etiqueta="Temperatura Ambiente"
          valor={`${TEMPERATURA} °C`}
        >
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
        </TarjetaMetrica>

        <TarjetaMetrica
          icono="wb-sunny"
          colorIcono={tema.onPrimaryContainer}
          fondoIcono={tema.primaryContainer}
          etiqueta="Radiación Lumínica"
          valor={`${LUZ.toLocaleString('es-MX')} lx`}
        >
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
        </TarjetaMetrica>

        <ResumenDeposito
          nivelDeposito={NIVEL_DEPOSITO}
          ultimoRiegoMl={planta.riegoSugeridoMl}
        />
      </ScrollView>
    </View>
  );
}
