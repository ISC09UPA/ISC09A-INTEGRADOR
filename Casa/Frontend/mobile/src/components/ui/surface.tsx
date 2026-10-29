import { BlurView } from 'expo-blur';
import type { ReactNode } from 'react';
import { Platform, StyleSheet, View, type StyleProp, type ViewProps, type ViewStyle } from 'react-native';

import { Glass, Radii, Spacing } from '@/theme';

export type SurfaceTone = 'glass' | 'strong' | 'subtle';

export type SurfaceProps = ViewProps & {
  tone?: SurfaceTone;
  /** Inner padding; pass 0/false-ish value to disable. */
  padding?: number;
  radius?: number;
  /** Set false to render a plain translucent fill without native blur. */
  blur?: boolean;
  children?: ReactNode;
  style?: StyleProp<ViewStyle>;
};

const fills: Record<SurfaceTone, string> = {
  glass: Glass.fill,
  strong: Glass.fillStrong,
  subtle: Glass.fillSubtle,
};

const shadows = Platform.select<ViewStyle>({
  ios: {
    shadowColor: Glass.shadowColor,
    shadowOpacity: Glass.shadowOpacity,
    shadowRadius: 16,
    shadowOffset: { width: 0, height: 8 },
  },
  android: { elevation: Glass.elevation },
  default: {},
});

/**
 * Frosted-glass card: native blur + translucent white fill + hairline border.
 */
export function Surface({
  tone = 'glass',
  padding = Spacing.three,
  radius = Radii.lg,
  blur = true,
  style,
  children,
  ...rest
}: SurfaceProps) {
  return (
      <View {...rest} style={[styles.shell, { borderRadius: radius }, shadows, style]}>
      {blur && (
        <BlurView
          intensity={Glass.blurIntensity}
          tint="light"
          style={[StyleSheet.absoluteFill, { borderRadius: radius }]}
        />
      )}
      <View
        pointerEvents="none"
        style={[StyleSheet.absoluteFill, { borderRadius: radius, backgroundColor: fills[tone] }]}
      />
      <View style={[styles.content, { borderRadius: radius, padding }]}>{children}</View>
    </View>
  );
}

const styles = StyleSheet.create({
  shell: {
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: Glass.border,
    backgroundColor: Platform.OS === 'android' ? 'rgba(255,255,255,0.25)' : undefined,
  },
  content: {
    overflow: 'hidden',
  },
});
