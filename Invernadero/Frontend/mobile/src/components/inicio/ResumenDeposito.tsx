
import { MaterialIcons } from '@expo/vector-icons';
import { StyleSheet, Text, View } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

interface Props {
  nivelDeposito: number;
  ultimoRiegoMl: number;
}

/** Fila de dos tarjetas: nivel del depósito y último riego. */
export function ResumenDeposito({
  nivelDeposito,
  ultimoRiegoMl,
}: Props) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <View style={s.fila}>
      <View
        style={[
          s.tarjeta,
          { backgroundColor: tema.surfaceContainerLowest },
        ]}
      >
        <MaterialIcons
          name="inventory-2"
          size={20}
          color={tema.onSecondaryContainer}
        />

        <Text style={[s.sub, { color: tema.onSurfaceVariant }]}>
          Depósito Principal
        </Text>

        <Text style={[s.valor, { color: tema.onSurface }]}>
          {nivelDeposito}%
        </Text>

        <View
          style={[
            s.barraFondo,
            { backgroundColor: tema.surfaceContainerHighest },
          ]}
        >
          <View
            style={[
              s.barra,
              {
                width: `${nivelDeposito}%`,
                backgroundColor: tema.secondary,
              },
            ]}
          />
        </View>
      </View>

      <View
        style={[
          s.tarjeta,
          { backgroundColor: tema.surfaceContainerLowest },
        ]}
      >
        <MaterialIcons
          name="schedule"
          size={20}
          color={tema.onPrimaryContainer}
        />

        <Text style={[s.sub, { color: tema.onSurfaceVariant }]}>
          Último Riego
        </Text>

        <Text style={[s.valor, { color: tema.onSurface }]}>
          {ultimoRiegoMl} mL
        </Text>

        <Text style={[s.sub, { color: tema.onSurfaceVariant }]}>
          Hoy, 08:30 h
        </Text>
      </View>
    </View>
  );
}

const s = StyleSheet.create({
  fila: { flexDirection: 'row', gap: 8 },
  tarjeta: {
    flex: 1,
    borderRadius: 16,
    padding: 16,
    gap: 8,
  },
  sub: { fontSize: 12 },
  valor: { fontSize: 20, fontWeight: '700' },
  barraFondo: {
    height: 8,
    borderRadius: 4,
    overflow: 'hidden',
  },
  barra: {
    height: '100%',
    borderRadius: 4,
  },
});
