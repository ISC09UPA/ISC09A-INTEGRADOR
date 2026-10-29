import { SymbolView } from 'expo-symbols';
import { StyleSheet, Text, View } from 'react-native';

import { LightingColors, LightingSizes, LightingTypography } from '@/constants/lighting-theme';
import { Spacing } from '@/constants/theme';

type ScreenHeaderProps = {
  eyebrow?: string;
  title: string;
  subtitle?: string;
};

export function ScreenHeader({ eyebrow = 'Casa domótica', title, subtitle }: ScreenHeaderProps) {
  return (
    <View style={styles.container}>
      <View style={styles.eyebrowRow}>
        <SymbolView
          name={{ ios: 'house', android: 'home', web: 'home' }}
          size={LightingSizes.headerIcon}
          tintColor={LightingColors.accent}
        />
        <Text style={styles.eyebrow}>{eyebrow}</Text>
      </View>
      <Text accessibilityRole="header" style={styles.title}>
        {title}
      </Text>
      {subtitle ? <Text style={styles.subtitle}>{subtitle}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: Spacing.one,
  },
  eyebrowRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
    marginBottom: Spacing.two,
  },
  eyebrow: LightingTypography.eyebrow,
  title: LightingTypography.title,
  subtitle: LightingTypography.subtitle,
});
