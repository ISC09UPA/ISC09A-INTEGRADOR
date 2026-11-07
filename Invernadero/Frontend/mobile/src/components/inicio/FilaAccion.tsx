import type { ReactNode } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { colores } from '@/theme/tokens';

interface Props {
  titulo: string;
  subtitulo: string;
  /** Control a la derecha: Switch o botón. */
  children: ReactNode;
}

/** Fila de actuador: título + estado a la izquierda, control a la derecha. */
export function FilaAccion({ titulo, subtitulo, children }: Props) {
  return (
    <View style={s.fila}>
      <View style={{ flex: 1 }}>
        <Text style={s.titulo}>{titulo}</Text>
        <Text style={s.sub}>{subtitulo}</Text>
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
    backgroundColor: colores.surfaceContainerLow,
  },
  titulo: { fontSize: 12, fontWeight: '600', color: colores.onSurface },
  sub: { fontSize: 12, color: colores.onSurfaceVariant },
});
