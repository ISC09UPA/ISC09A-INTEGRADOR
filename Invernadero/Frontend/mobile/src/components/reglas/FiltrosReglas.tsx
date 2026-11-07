import { Pressable, ScrollView, StyleSheet, Text } from 'react-native';
import { colores } from '@/theme/tokens';
import { FILTROS, type Categoria } from './datos';

interface Props {
  seleccionado: Categoria | 'todas';
  onSelect: (id: Categoria | 'todas') => void;
}

/** Chips horizontales para filtrar reglas por categoría. */
export function FiltrosReglas({ seleccionado, onSelect }: Props) {
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={s.fila}>
      {FILTROS.map((f) => {
        const sel = f.id === seleccionado;
        return (
          <Pressable
            key={f.id}
            onPress={() => onSelect(f.id)}
            style={[s.chip, { backgroundColor: sel ? colores.primary : colores.surfaceContainerLowest }]}
          >
            <Text style={[s.texto, { color: sel ? colores.onPrimary : colores.onSurface }]}>{f.etiqueta}</Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

const s = StyleSheet.create({
  fila: { gap: 6, paddingVertical: 4 },
  chip: { borderRadius: 999, paddingHorizontal: 12, paddingVertical: 6 },
  texto: { fontSize: 13, fontWeight: '600' },
});
