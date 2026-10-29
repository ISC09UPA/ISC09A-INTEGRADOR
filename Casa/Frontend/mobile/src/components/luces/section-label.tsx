import { StyleSheet, Text, type StyleProp, type TextStyle } from 'react-native';

import { LightingTypography } from '@/constants/lighting-theme';

type SectionLabelProps = {
  children: React.ReactNode;
  style?: StyleProp<TextStyle>;
};

export function SectionLabel({ children, style }: SectionLabelProps) {
  return (
    <Text accessibilityRole="header" style={[styles.label, style]}>
      {children}
    </Text>
  );
}

const styles = StyleSheet.create({
  label: LightingTypography.label,
});
