import { Image } from 'expo-image';
import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { AppText } from './text';

import { FontFamily, Palette, Radii } from '@/theme';

export type AvatarProps = {
  /** initials or image source; at least one should be provided. */
  name?: string;
  uri?: string;
  size?: number;
  /** Ring color around the avatar. */
  ring?: boolean;
  style?: StyleProp<ViewStyle>;
};

/** Circular avatar: photo when available, gradient initials otherwise. */
export function Avatar({ name, uri, size = 40, ring = true, style }: AvatarProps) {
  const initials =
    name
      ?.trim()
      .split(/\s+/)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('') ?? '?';

  return (
    <View
      style={[
        styles.wrap,
        { width: size, height: size, borderRadius: size / 2 },
        ring && styles.ring,
        style,
      ]}>
      {uri ? (
        <Image source={{ uri }} style={styles.image} contentFit="cover" accessibilityLabel={name} />
      ) : (
        <View style={[styles.fallback, { borderRadius: size / 2 }]}>
          <AppText style={{ fontFamily: FontFamily.displaySemiBold, fontSize: size * 0.36, color: Palette.white }}>
            {initials}
          </AppText>
        </View>
      )}
    </View>
  );
}

export type AvatarGroupProps = {
  users: { name: string; uri?: string }[];
  size?: number;
  max?: number;
};

/** Overlapping avatars with a "+N" overflow bubble. */
export function AvatarGroup({ users, size = 32, max = 3 }: AvatarGroupProps) {
  const visible = users.slice(0, max);
  const overflow = users.length - visible.length;

  return (
    <View style={styles.group}>
      {visible.map((user, index) => (
        <Avatar
          key={user.name}
          name={user.name}
          uri={user.uri}
          size={size}
          style={[styles.groupItem, { marginLeft: index === 0 ? 0 : -size * 0.35 }]}
        />
      ))}
      {overflow > 0 && (
        <View
          style={[
            styles.overflow,
            { width: size, height: size, borderRadius: size / 2, marginLeft: -size * 0.35 },
          ]}>
          <AppText variant="caption" color="white">
            +{overflow}
          </AppText>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    overflow: 'hidden',
    backgroundColor: Palette.neutralSoft,
  },
  ring: {
    borderWidth: 2,
    borderColor: 'rgba(255,255,255,0.9)',
  },
  image: {
    width: '100%',
    height: '100%',
  },
  fallback: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Palette.accent,
  },
  group: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  groupItem: {
    borderRadius: Radii.pill,
  },
  overflow: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Palette.inkSoft,
    borderWidth: 2,
    borderColor: 'rgba(255,255,255,0.9)',
  },
});
