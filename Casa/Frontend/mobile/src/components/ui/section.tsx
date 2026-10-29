import type { ReactNode } from 'react';
import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { AppText } from './text';

import { Spacing } from '@/theme';

export type SectionProps = {
  title?: string;
  /** Right-aligned node in the section header (e.g. a "Ver todo" button). */
  action?: ReactNode;
  children: ReactNode;
  gap?: number;
  style?: StyleProp<ViewStyle>;
};

/** Titled content block used to group cards down a screen. */
export function Section({ title, action, children, gap = Spacing.two, style }: SectionProps) {
  return (
    <View style={[styles.section, style]}>
      {(title || action) && (
        <View style={styles.header}>
          {title ? (
            <AppText variant="headline" style={styles.title}>
              {title}
            </AppText>
          ) : (
            <View />
          )}
          {action}
        </View>
      )}
      <View style={{ gap }}>{children}</View>
    </View>
  );
}

const styles = StyleSheet.create({
  section: {
    gap: Spacing.two,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: Spacing.two,
    paddingHorizontal: Spacing.half,
  },
  title: {
    flexShrink: 1,
  },
});
