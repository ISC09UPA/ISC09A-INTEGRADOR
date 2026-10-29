import { StyleSheet, Text, View } from 'react-native';

import {
  LightingColors,
  LightingRadii,
  LightingSizes,
  LightingTypography,
} from '@/constants/lighting-theme';
import { Spacing } from '@/constants/theme';

type PendingBannerProps = {
  title?: string;
  message?: string;
};

export function PendingBanner({
  title = 'Orden pendiente',
  message = 'Esperando confirmación de la casa.',
}: PendingBannerProps) {
  return (
    <View
      accessibilityRole="alert"
      accessibilityLiveRegion="polite"
      accessibilityLabel={`${title}. ${message}`}
      style={styles.banner}>
      <Text style={styles.text}>{title}</Text>
      <Text style={styles.text}>{message}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  banner: {
    backgroundColor: LightingColors.warningBg,
    borderColor: LightingColors.glassBorder,
    borderWidth: LightingSizes.borderWidth,
    borderRadius: LightingRadii.banner,
    paddingHorizontal: Spacing.four,
    paddingVertical: Spacing.three,
    gap: Spacing.one,
  },
  text: {
    ...LightingTypography.body,
    color: LightingColors.warningText,
  },
});
