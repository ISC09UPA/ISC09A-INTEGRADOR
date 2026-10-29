import { StyleSheet, View } from 'react-native';
import { LinearGradient, type LinearGradientProps } from 'expo-linear-gradient';

const capas: Pick<LinearGradientProps, 'colors' | 'start' | 'end'>[] = [
  { colors: ['#EEF1F8', '#ECEEF7', '#DDDAF3'] },
  { colors: ['rgba(150, 208, 228, 0.95)', 'rgba(150, 208, 228, 0)'], start: { x: 1, y: 0 }, end: { x: 0.35, y: 0.45 } },
  { colors: ['rgba(186, 176, 238, 0.9)', 'rgba(186, 176, 238, 0)'], start: { x: 0, y: 0.85 }, end: { x: 0.7, y: 0.5 } },
];

export function Background() {
  return (
    <View style={StyleSheet.absoluteFill} pointerEvents="none">
      {capas.map((capa, i) => (
        <LinearGradient key={i} {...capa} style={StyleSheet.absoluteFill} />
      ))}
    </View>
  );
}
