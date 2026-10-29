import type { ReactNode } from 'react';
import { StyleSheet, View } from 'react-native';

import { Icon, Icons } from './icon';
import { AppText } from './text';

import { LinearGradient } from 'expo-linear-gradient';

import { FontFamily, Gradients, Spacing } from '@/theme';

/** Rounded gradient tile with the house glyph — the app's brand mark. */
export function BrandMark({ size = 36 }: { size?: number }) {
  return (
    <LinearGradient
      colors={[...Gradients.accent]}
      start={{ x: 0, y: 0 }}
      end={{ x: 1, y: 1 }}
      style={[styles.mark, { width: size, height: size, borderRadius: size * 0.32 }]}>
      <Icon source={Icons.home} size={size * 0.55} color="#FFFFFF" />
    </LinearGradient>
  );
}

export type BrandProps = {
  size?: number;
  color?: string;
};

/** Brand mark + wordmark. */
export function Brand({ size = 36, color = '#12263A' }: BrandProps) {
  return (
    <View style={styles.brandRow}>
      <BrandMark size={size} />
      <AppText
        style={{
          fontFamily: FontFamily.displayBold,
          fontSize: size * 0.55,
          color,
        }}>
        Casa
      </AppText>
    </View>
  );
}

export type AppHeaderProps = {
  /** Small label above the title, e.g. a greeting or section. */
  eyebrow?: string;
  title: string;
  subtitle?: string;
  /** When provided, the brand row replaces the eyebrow. */
  showBrand?: boolean;
  /** Right-aligned action area (buttons, avatars…). */
  right?: ReactNode;
};

/** Screen header: brand/eyebrow + large title + optional subtitle and action slot. */
export function AppHeader({ eyebrow, title, subtitle, showBrand = false, right }: AppHeaderProps) {
  return (
    <View style={styles.header}>
      <View style={styles.headerText}>
        {showBrand ? (
          <View style={styles.eyebrowRow}>
            <Brand size={28} />
          </View>
        ) : eyebrow ? (
          <AppText variant="caption" color="accent" style={styles.eyebrow}>
            {eyebrow.toUpperCase()}
          </AppText>
        ) : null}
        <AppText variant="display">{title}</AppText>
        {subtitle ? (
          <AppText variant="body" color="inkSoft">
            {subtitle}
          </AppText>
        ) : null}
      </View>
      {right ? <View style={styles.headerRight}>{right}</View> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  mark: {
    alignItems: 'center',
    justifyContent: 'center',
  },
  brandRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
    gap: Spacing.two,
    paddingTop: Spacing.two,
    paddingBottom: Spacing.one,
  },
  headerText: {
    flex: 1,
    gap: Spacing.half,
  },
  eyebrowRow: {
    marginBottom: Spacing.one,
  },
  eyebrow: {
    letterSpacing: 1.1,
    color: '#1D5FA8',
  },
  headerRight: {
    alignItems: 'flex-end',
    gap: Spacing.one,
    paddingTop: Spacing.one,
  },
});
