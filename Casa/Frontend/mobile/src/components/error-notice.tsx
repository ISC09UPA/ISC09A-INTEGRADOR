import type { PropsWithChildren } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { text } from '@/constants/casa-theme';

/** Aviso de error en tono rojo suave. */
export function ErrorNotice({ children }: PropsWithChildren) {
  return (
    <View style={styles.notice} accessibilityRole="alert">
      <Text style={styles.text}>{children}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  notice: {
    marginTop: 12,
    paddingHorizontal: 14,
    paddingVertical: 14,
    borderRadius: 22,
    backgroundColor: 'rgba(244, 214, 220, 0.85)',
    borderWidth: 1.5,
    borderColor: 'rgba(255, 255, 255, 0.9)',
  },
  text: { ...text('regular', 14), color: '#A3272E', lineHeight: 20 },
});
