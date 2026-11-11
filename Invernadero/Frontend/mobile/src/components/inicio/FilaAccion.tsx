
import type { ReactNode } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

interface Props {
  titulo: string;
  subtitulo: string;
  /** Control a la derecha: Switch o botón. */
  children: ReactNode;
}

/** Fila de actuador: título + estado a la izquierda, control a la derecha. */
export function FilaAccion({ titulo, subtitulo, children }: Props) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <View style={[s.fila, { backgroundColor: tema.surfaceContainerLow }]}>
      <View style={{ flex: 1 }}>
        <Text style={[s.titulo, { color: tema.onSurface }]}>
          {titulo}
        </Text>

        <Text style={[s.sub, { color: tema.onSurfaceVariant }]}>
          {subtitulo}
        </Text>
      </View>

      {children}
    </View>
  );
}

const s = StyleSheet.create({
  fila: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    borderRadius: 12,
    padding: 10,
  },
  titulo: { fontSize: 12, fontWeight: '600' },
  sub: { fontSize: 12 },
});
