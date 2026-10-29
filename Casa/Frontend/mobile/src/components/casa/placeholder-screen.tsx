import { LinearGradient } from 'expo-linear-gradient';
import { SymbolView } from 'expo-symbols';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Casa } from '@/constants/casa';

type PlaceholderScreenProps = {
  title: string;
  description: string;
};

export function PlaceholderScreen({ title, description }: PlaceholderScreenProps) {
  const insets = useSafeAreaInsets();

  return (
    <View style={styles.root}>
      <LinearGradient
        colors={Casa.gradient}
        start={{ x: 0.05, y: 0 }}
        end={{ x: 0.95, y: 1 }}
        style={StyleSheet.absoluteFill}
      />
      <ScrollView
        showsVerticalScrollIndicator={false}
        contentContainerStyle={[
          styles.content,
          { paddingTop: insets.top + 14, paddingBottom: insets.bottom + Casa.tabBar.clearance },
        ]}>
        <View style={styles.inner}>
          <View style={styles.eyebrowRow}>
            <SymbolView
              name={{ ios: 'house', android: 'home', web: 'home' }}
              size={13}
              tintColor={Casa.colors.tealDark}
            />
            <Text style={styles.eyebrow}>CASA DOMÓTICA</Text>
          </View>

          <Text style={styles.title}>{title}</Text>
          <Text style={styles.subtitle}>{description}</Text>

          <View style={styles.card}>
            <Text style={styles.cardText}>Esta sección estará disponible pronto.</Text>
          </View>
        </View>
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: '#E4EBFA',
    overflow: 'hidden',
  },
  content: {
    flexGrow: 1,
    paddingHorizontal: 20,
    alignItems: 'center',
  },
  inner: {
    width: '100%',
    maxWidth: 520,
  },
  eyebrowRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  eyebrow: {
    fontSize: 10.5,
    lineHeight: 14,
    fontWeight: '700',
    letterSpacing: 1.4,
    color: Casa.colors.label,
  },
  title: {
    marginTop: 10,
    fontSize: 32,
    lineHeight: 38,
    fontWeight: '700',
    letterSpacing: -0.6,
    color: Casa.colors.text,
  },
  subtitle: {
    marginTop: 4,
    fontSize: 13,
    lineHeight: 18,
    fontWeight: '500',
    color: Casa.colors.textMuted,
  },
  card: {
    marginTop: 20,
    borderRadius: Casa.radius.card,
    borderWidth: 1,
    borderColor: Casa.colors.surfaceBorder,
    backgroundColor: Casa.colors.surface,
    paddingHorizontal: 18,
    paddingVertical: 20,
    shadowColor: '#53627F',
    shadowOpacity: 0.08,
    shadowRadius: 18,
    shadowOffset: { width: 0, height: 8 },
    elevation: 3,
  },
  cardText: {
    fontSize: 14,
    lineHeight: 20,
    fontWeight: '500',
    color: Casa.colors.textMuted,
  },
});
