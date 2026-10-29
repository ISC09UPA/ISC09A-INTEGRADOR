import type { PropsWithChildren } from 'react';
import { StyleSheet, Text } from 'react-native';
import { text } from '@/constants/casa-theme';

export function SectionLabel({ children }: PropsWithChildren) {
  return <Text style={styles.sectionLabel}>{children}</Text>;
}

const styles = StyleSheet.create({
  sectionLabel: { ...text('medium', 13, 'muted'), letterSpacing: 0.6, textTransform: 'uppercase', marginTop: 28, marginBottom: 12 },
});
