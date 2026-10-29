import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Feather } from '@expo/vector-icons';
import { colors, text, type IconName } from '@/constants/casa-theme';
import { useCasaTheme } from '@/hooks/use-casa-theme';

export interface QuickActionProps {
  icon: IconName;
  label: string;
  activo: boolean;
  onPress?: () => void;
}

export function QuickAction({ icon, label, activo, onPress }: QuickActionProps) {
  const theme = useCasaTheme();

  return (
    <Pressable style={styles.quick} onPress={onPress}>
      {({ pressed }) => (
        <>
          <View style={[styles.quickIcon, activo && styles.quickIconActive, pressed && { transform: [{ scale: 0.94 }] }]}>
            <Feather name={icon} size={22} color={activo ? theme.white : theme.ink} />
          </View>
          <Text style={styles.quickLabel} numberOfLines={2}>{label}</Text>
        </>
      )}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  quick: { width: '23%', alignItems: 'center' },
  quickIcon: {
    width: 56,
    height: 56,
    borderRadius: 28,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.8)',
    borderWidth: 1,
    borderColor: 'rgba(22, 34, 46, 0.06)',
  },
  quickIconActive: { backgroundColor: colors.teal, borderColor: colors.teal },
  quickLabel: { ...text('regular', 12), lineHeight: 16, textAlign: 'center', marginTop: 8 },
});
