import type { ReactNode } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { colors, font } from '@/constants/tokens';

export default function Seccion({ titulo, children }: { titulo: string; children: ReactNode }) {
  return (
    <View style={{ gap: 12 }}>
      <Text style={s.titulo}>{titulo}</Text>
      {children}
    </View>
  );
}

const s = StyleSheet.create({
  titulo: { fontFamily: font.bold, fontSize: 18, color: colors.texto },
});
