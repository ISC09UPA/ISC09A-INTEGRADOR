import { useEffect, useState } from 'react';
import { Animated, Text, View } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';

import { TAM_BOTON, ZONA_DY, ZONA_RADIO } from './constantes';
import { useTema } from './useTema';

interface Props {
  /** Aparece cuando el usuario empieza a arrastrar hacia arriba. */
  visible: boolean;
  /** true cuando el dedo está sobre la zona: soltar aquí cancela. */
  activa: boolean;
}

/**
 * Zona circular roja con una X. Se posiciona de forma absoluta respecto al
 * botón de micrófono (su contenedor), por lo que la geometría del gesto y la
 * de la zona siempre coinciden.
 */
export function ZonaCancelacion({ visible, activa }: Props) {
  const tema = useTema();
  const [opacidad] = useState(() => new Animated.Value(0));
  const [escala] = useState(() => new Animated.Value(1));

  useEffect(() => {
    Animated.timing(opacidad, {
      toValue: visible ? 1 : 0,
      duration: 140,
      useNativeDriver: true,
    }).start();
  }, [visible, opacidad]);

  useEffect(() => {
    Animated.spring(escala, {
      toValue: activa ? 1.3 : 1,
      friction: 6,
      useNativeDriver: true,
    }).start();
  }, [activa, escala]);

  const arriba = TAM_BOTON / 2 - ZONA_DY - ZONA_RADIO;

  return (
    <Animated.View
      pointerEvents="none"
      style={{ position: 'absolute', opacity: opacidad, left: 0, top: 0, width: TAM_BOTON }}
    >
      {/* Indicación de texto */}
      <View
        style={{
          position: 'absolute',
          top: arriba - 40,
          left: TAM_BOTON / 2 - 120,
          width: 240,
          alignItems: 'center',
        }}
      >
        <View
          style={{
            paddingHorizontal: 12,
            paddingVertical: 6,
            borderRadius: 999,
            backgroundColor: activa ? tema.error : tema.errorContainer,
          }}
        >
          <Text
            style={{
              fontSize: 12,
              fontWeight: '600',
              color: activa ? tema.onError : tema.onErrorContainer,
            }}
          >
            {activa ? 'Suelta para cancelar' : 'Desliza hacia arriba para cancelar'}
          </Text>
        </View>
      </View>

      {/* Círculo rojo con X */}
      <Animated.View
        style={{
          position: 'absolute',
          top: arriba,
          left: TAM_BOTON / 2 - ZONA_RADIO,
          width: ZONA_RADIO * 2,
          height: ZONA_RADIO * 2,
          borderRadius: ZONA_RADIO,
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: activa ? tema.error : tema.errorContainer,
          borderWidth: 2,
          borderColor: tema.error,
          transform: [{ scale: escala }],
          shadowColor: '#000',
          shadowOpacity: activa ? 0.3 : 0.15,
          shadowRadius: 6,
          shadowOffset: { width: 0, height: 2 },
          elevation: activa ? 8 : 4,
        }}
      >
        <MaterialIcons
          name="close"
          size={activa ? 30 : 26}
          color={activa ? tema.onError : tema.error}
        />
      </Animated.View>
    </Animated.View>
  );
}
