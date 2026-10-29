import { LinearGradient } from 'expo-linear-gradient';
import { Pressable, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { Icon, Icons } from '../ui/icon';
import { AppText } from '../ui/text';

import { Gradients, Palette, Radii, Spacing } from '@/theme';

export type CameraCardProps = {
  name: string;
  /** e.g. "En vivo" or "Grabando". */
  status?: string;
  live?: boolean;
  /** Timestamp of the latest snapshot/clip. */
  time?: string;
  onPress?: () => void;
  style?: StyleProp<ViewStyle>;
};

/** Camera feed tile — dark stylized placeholder frame with live badge. */
export function CameraCard({ name, status = 'En vivo', live = true, time, onPress, style }: CameraCardProps) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`Cámara ${name}`}
      onPress={onPress}
      style={({ pressed }) => [styles.card, pressed && styles.pressed, style]}>
      <LinearGradient colors={[...Gradients.night]} style={styles.feed}>
        {/* stylized frame guides */}
        <View style={[styles.guide, styles.guideTop]} />
        <View style={[styles.guide, styles.guideBottom]} />
        <View style={styles.lens}>
          <Icon source={Icons.camera} size={22} color="rgba(255,255,255,0.5)" />
        </View>

        <View style={styles.topRow}>
          <View style={[styles.badge, live ? styles.badgeLive : styles.badgeIdle]}>
            {live && <View style={styles.dot} />}
            <AppText variant="caption" color="white">
              {status}
            </AppText>
          </View>
          {time ? (
            <View style={styles.timeBadge}>
              <AppText variant="caption" color="white">
                {time}
              </AppText>
            </View>
          ) : null}
        </View>
      </LinearGradient>
      <AppText variant="callout" style={styles.name}>
        {name}
      </AppText>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  card: {
    gap: Spacing.one,
  },
  pressed: {
    opacity: 0.8,
  },
  feed: {
    aspectRatio: 16 / 10,
    borderRadius: Radii.lg,
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: 'rgba(255,255,255,0.35)',
  },
  guide: {
    position: 'absolute',
    left: 0,
    right: 0,
    height: StyleSheet.hairlineWidth,
    backgroundColor: 'rgba(255,255,255,0.14)',
  },
  guideTop: { top: '33%' },
  guideBottom: { top: '66%' },
  lens: {
    width: 52,
    height: 52,
    borderRadius: Radii.pill,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(255,255,255,0.08)',
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: 'rgba(255,255,255,0.2)',
  },
  topRow: {
    position: 'absolute',
    top: Spacing.two,
    left: Spacing.two,
    right: Spacing.two,
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  badge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.half,
    paddingVertical: Spacing.half,
    paddingHorizontal: Spacing.two,
    borderRadius: Radii.pill,
  },
  badgeLive: {
    backgroundColor: 'rgba(207, 83, 64, 0.9)',
  },
  badgeIdle: {
    backgroundColor: 'rgba(255,255,255,0.2)',
  },
  dot: {
    width: 6,
    height: 6,
    borderRadius: 3,
    backgroundColor: Palette.white,
  },
  timeBadge: {
    paddingVertical: Spacing.half,
    paddingHorizontal: Spacing.two,
    borderRadius: Radii.pill,
    backgroundColor: 'rgba(0,0,0,0.35)',
  },
  name: {
    paddingHorizontal: Spacing.one,
  },
});
