import { LinearGradient } from 'expo-linear-gradient';
import { StyleSheet, View } from 'react-native';
import Svg, { Defs, RadialGradient, Rect, Stop } from 'react-native-svg';

import { LightingColors } from '@/constants/lighting-theme';

// Fondo con gradiente base y dos manchas difuminadas (radial gradients).
export function ScreenBackground({ children }: { children: React.ReactNode }) {
  return (
    <View style={styles.root}>
      <LinearGradient
        colors={[
          LightingColors.backgroundTop,
          LightingColors.background,
          LightingColors.backgroundBottom,
        ]}
        style={StyleSheet.absoluteFill}
      />
      <Svg style={StyleSheet.absoluteFill} width="100%" height="100%" pointerEvents="none">
        <Defs>
          <RadialGradient id="tealBlob" cx="92%" cy="6%" rx="75%" ry="38%">
            <Stop offset="0" stopColor={LightingColors.blobTeal} stopOpacity="0.95" />
            <Stop offset="1" stopColor={LightingColors.blobTeal} stopOpacity="0" />
          </RadialGradient>
          <RadialGradient id="lavenderBlob" cx="0%" cy="48%" rx="70%" ry="32%">
            <Stop offset="0" stopColor={LightingColors.blobLavender} stopOpacity="0.9" />
            <Stop offset="1" stopColor={LightingColors.blobLavender} stopOpacity="0" />
          </RadialGradient>
        </Defs>
        <Rect width="100%" height="100%" fill="url(#tealBlob)" />
        <Rect width="100%" height="100%" fill="url(#lavenderBlob)" />
      </Svg>
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: LightingColors.background,
  },
});
