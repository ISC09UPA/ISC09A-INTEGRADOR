import { useEffect, useRef, useState } from 'react';
import { Animated, Easing, PanResponder, StyleSheet, View } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';
import { router } from 'expo-router';

import { hapticoAviso, hapticoError, hapticoToque } from '../../../utils/haptico';
import { useVoz } from '@/voz/VozProvider';
import {
  RADIO_ACTIVACION,
  TAM_BOTON,
  UMBRAL_MOSTRAR,
  UMBRAL_OCULTAR,
  ZONA_DY,
} from './constantes';
import { useTema } from './useTema';
import { ZonaCancelacion } from './ZonaCancelacion';

/**
 * Botón global de micrófono (Push to Talk). Vive en la barra de pestañas, que
 * permanece montada al cambiar de pestaña, por lo que el gesto no se interrumpe
 * cuando la navegación salta a "Voz" mientras el dedo sigue presionando.
 *
 * Se usa PanResponder (React Native core) con `onPanResponderTerminationRequest`
 * en false: una vez concedido el gesto, ni un ScrollView ni el navegador pueden
 * "robarlo" y cortar la grabación.
 */
export function BotonMicrofono() {
  const tema = useTema();
  const {
    estado,
    gesto,
    iniciarGrabacion,
    finalizarGrabacion,
    cancelarGrabacion,
    actualizarGesto,
  } = useVoz();

  // Estado del gesto en curso (solo se toca desde los manejadores del gesto).
  const gestoActivoRef = useRef(false);
  const visibleRef = useRef(false);
  const activaRef = useRef(false);

  const [pulso] = useState(() => new Animated.Value(0));
  const [escala] = useState(() => new Animated.Value(1));
  const grabando = estado === 'recording';

  const [panHandlers, setPanHandlers] = useState<
    ReturnType<typeof PanResponder.create>['panHandlers'] | null
  >(null);

  useEffect(() => {
    /** Calcula el estado de la zona de cancelación a partir del desplazamiento. */
    const evaluar = (dx: number, dy: number) => {
      const distanciaZona = Math.hypot(dx, dy + ZONA_DY);
      const activa = distanciaZona <= RADIO_ACTIVACION;
      const umbral = visibleRef.current ? UMBRAL_OCULTAR : UMBRAL_MOSTRAR;
      const visible = activa || dy < -umbral;
      return { visible, activa };
    };

    const limpiarGesto = () => {
      visibleRef.current = false;
      activaRef.current = false;
      actualizarGesto(false, false);
    };

    const terminar = (desplazamiento?: { dx: number; dy: number }) => {
      if (!gestoActivoRef.current) {
        limpiarGesto();
        return;
      }
      gestoActivoRef.current = false;

      // Soltar dentro de la zona = cancelar. Fuera = finalizar.
      const cancelar = desplazamiento
        ? evaluar(desplazamiento.dx, desplazamiento.dy).activa
        : false;
      limpiarGesto();

      if (cancelar) {
        hapticoError();
        void cancelarGrabacion();
      } else {
        hapticoToque();
        void finalizarGrabacion();
      }
    };

    const responder = PanResponder.create({
      onStartShouldSetPanResponder: () => true,
      onMoveShouldSetPanResponder: () => true,
      // Nadie (ScrollView, navegación, etc.) puede quitarle el gesto al botón.
      onPanResponderTerminationRequest: () => false,
      onShouldBlockNativeResponder: () => true,

      onPanResponderGrant: () => {
        hapticoToque();
        gestoActivoRef.current = iniciarGrabacion();
        // Navega de inmediato a Voz; el botón sigue montado y el gesto continúa.
        router.navigate('/voz');
      },

      onPanResponderMove: (_evento, g) => {
        if (!gestoActivoRef.current) return;
        const { visible, activa } = evaluar(g.dx, g.dy);
        if (activa && !activaRef.current) hapticoAviso();
        visibleRef.current = visible;
        activaRef.current = activa;
        actualizarGesto(visible, activa);
      },

      onPanResponderRelease: (_evento, g) => terminar({ dx: g.dx, dy: g.dy }),

      // Interrupción del sistema (llamada, gesto del SO...): se conserva la
      // grabación en lugar de perderla, nunca se cancela por accidente.
      onPanResponderTerminate: () => terminar(),
    });
    setPanHandlers(responder.panHandlers);
  }, [actualizarGesto, cancelarGrabacion, finalizarGrabacion, iniciarGrabacion]);

  // Pulso suave mientras se graba.
  useEffect(() => {
    if (!grabando) {
      pulso.setValue(0);
      return;
    }
    const animacion = Animated.loop(
      Animated.timing(pulso, {
        toValue: 1,
        duration: 1100,
        easing: Easing.out(Easing.quad),
        useNativeDriver: true,
      })
    );
    animacion.start();
    return () => animacion.stop();
  }, [grabando, pulso]);

  useEffect(() => {
    Animated.spring(escala, {
      toValue: grabando ? 1.12 : 1,
      friction: 6,
      useNativeDriver: true,
    }).start();
  }, [grabando, escala]);

  const sobreZona = grabando && gesto.zonaActiva;
  const fondo = grabando ? tema.error : tema.primary;
  const iconoColor = grabando ? tema.onError : tema.onPrimary;

  return (
    <View style={styles.contenedor}>
      <View
        {...panHandlers}
        style={styles.area}
        accessible
        accessibilityRole="button"
        accessibilityLabel="Micrófono"
        accessibilityHint="Mantén presionado para hablar. Suelta para terminar. Desliza hacia arriba para cancelar."
      >
        <ZonaCancelacion visible={grabando && gesto.zonaVisible} activa={sobreZona} />

        {/* Aro pulsante */}
        <Animated.View
          pointerEvents="none"
          style={[
            styles.circulo,
            {
              backgroundColor: tema.error,
              opacity: pulso.interpolate({ inputRange: [0, 1], outputRange: [grabando ? 0.45 : 0, 0] }),
              transform: [
                { scale: pulso.interpolate({ inputRange: [0, 1], outputRange: [1, 2] }) },
              ],
            },
          ]}
        />

        {/* Botón */}
        <Animated.View
          pointerEvents="none"
          style={[
            styles.circulo,
            styles.boton,
            { backgroundColor: fondo, transform: [{ scale: escala }] },
          ]}
        >
          <MaterialIcons name={sobreZona ? 'close' : 'mic'} size={28} color={iconoColor} />
        </Animated.View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  // Ocupa el hueco de la pestaña "Voz" y sobresale de la barra hacia arriba.
  contenedor: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'flex-start',
  },
  area: {
    width: TAM_BOTON,
    height: TAM_BOTON,
    marginTop: -20,
  },
  circulo: {
    position: 'absolute',
    top: 0,
    left: 0,
    width: TAM_BOTON,
    height: TAM_BOTON,
    borderRadius: TAM_BOTON / 2,
  },
  boton: {
    alignItems: 'center',
    justifyContent: 'center',
    shadowColor: '#000',
    shadowOpacity: 0.25,
    shadowRadius: 6,
    shadowOffset: { width: 0, height: 3 },
    elevation: 6,
  },
});
