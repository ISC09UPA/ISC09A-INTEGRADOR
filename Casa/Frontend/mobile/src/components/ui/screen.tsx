import { LinearGradient } from 'expo-linear-gradient';
import { BlurView } from 'expo-blur';
import type { ReactNode } from 'react';
import { ScrollView, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';
import { SafeAreaView, type Edge } from 'react-native-safe-area-context';

import { Gradients, MaxContentWidth, Spacing } from '@/theme';

/** Height reserved for the floating tab bar + breathing room. */
export const TabBarContentInset = 120;

export type ScreenProps = {
  children: ReactNode;
  /** 'gradient' renders the sky gradient here; 'transparent' expects it from a parent layout. */
  background?: 'gradient' | 'transparent';
  /** Render children inside a ScrollView. */
  scroll?: boolean;
  /** Clear the floating tab bar area. */
  tabBarInset?: boolean;
  /** Reserve bottom safe-area space (screens pushed over the tab bar). */
  includeBottomInset?: boolean;
  edges?: Edge[];
  contentContainerStyle?: StyleProp<ViewStyle>;
  style?: StyleProp<ViewStyle>;
};

/** Full-screen canvas with the app's signature sky gradient. */
export function Screen({
  children,
  background = 'gradient',
  scroll = true,
  tabBarInset = false,
  includeBottomInset = false,
  edges = ['top', 'left', 'right'],
  contentContainerStyle,
  style,
}: ScreenProps) {
  const content = (
    <View
      style={[
        styles.content,
        tabBarInset && styles.contentTabBarInset,
        includeBottomInset && styles.contentBottomInset,
        contentContainerStyle,
        !scroll && style,
      ]}>
      {children}
    </View>
  );

  const canvas = (
    <SafeAreaView edges={edges} style={styles.safeArea}>
      {scroll ? (
        <ScrollView
          style={styles.scroll}
          contentContainerStyle={[
            styles.content,
            tabBarInset && styles.contentTabBarInset,
            includeBottomInset && styles.contentBottomInset,
            contentContainerStyle,
          ]}
          showsVerticalScrollIndicator={false}
          keyboardShouldPersistTaps="handled">
          {children}
        </ScrollView>
      ) : (
        content
      )}
    </SafeAreaView>
  );

  if (background === 'transparent') {
    return (
      <View style={styles.gradient}>
        <AmbientGlow />
        {canvas}
      </View>
    );
  }

  return (
    <LinearGradient colors={[...Gradients.screen]} style={styles.gradient}>
      <AmbientGlow />
      {canvas}
    </LinearGradient>
  );
}

function AmbientGlow() {
  return (
    <View pointerEvents="none" style={StyleSheet.absoluteFill}>
      <BlurView intensity={70} tint="light" style={[styles.glow, styles.skyGlow]}>
        <View style={styles.skyColor} />
      </BlurView>
      <BlurView intensity={70} tint="light" style={[styles.glow, styles.lilacGlow]}>
        <View style={styles.lilacColor} />
      </BlurView>
    </View>
  );
}

const styles = StyleSheet.create({
  gradient: {
    flex: 1,
  },
  safeArea: {
    flex: 1,
  },
  scroll: {
    flex: 1,
  },
  content: {
    width: '100%',
    maxWidth: MaxContentWidth,
    alignSelf: 'center',
    paddingHorizontal: 20,
    paddingTop: Spacing.two,
    paddingBottom: Spacing.four,
    gap: Spacing.three,
  },
  contentTabBarInset: {
    paddingBottom: TabBarContentInset,
  },
  contentBottomInset: {
    paddingBottom: Spacing.six,
  },
  glow: {
    position: 'absolute',
    width: 310,
    height: 330,
    borderRadius: 200,
    overflow: 'hidden',
  },
  skyGlow: {
    top: -60,
    right: -115,
  },
  lilacGlow: {
    bottom: 0,
    left: -110,
  },
  skyColor: {
    flex: 1,
    backgroundColor: 'rgba(143,204,221,0.24)',
  },
  lilacColor: {
    flex: 1,
    backgroundColor: 'rgba(188,178,240,0.20)',
  },
});
