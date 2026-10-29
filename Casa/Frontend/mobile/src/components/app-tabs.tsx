import { Tabs, TabList, TabSlot, TabTrigger, TabTriggerSlotProps } from 'expo-router/ui';
import { SymbolView } from 'expo-symbols';
import { Pressable, StyleSheet, Text, View, type PressableStateCallbackType } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { Casa } from '@/constants/casa';

const TABS = [
  {
    name: 'inicio',
    href: '/inicio',
    label: 'Inicio',
    icon: { ios: 'house', android: 'home', web: 'home' },
    iconFocused: { ios: 'house.fill', android: 'home', web: 'home' },
  },
  {
    name: 'luces',
    href: '/luces',
    label: 'Luces',
    icon: { ios: 'lightbulb', android: 'lightbulb', web: 'lightbulb' },
    iconFocused: { ios: 'lightbulb.fill', android: 'lightbulb', web: 'lightbulb' },
  },
  {
    name: 'seguridad',
    href: '/',
    label: 'Seguridad',
    icon: { ios: 'shield', android: 'shield', web: 'shield' },
    iconFocused: { ios: 'shield.fill', android: 'shield', web: 'shield' },
  },
  {
    name: 'camara',
    href: '/camara',
    label: 'Cámara',
    icon: { ios: 'video', android: 'videocam', web: 'videocam' },
    iconFocused: { ios: 'video.fill', android: 'videocam', web: 'videocam' },
  },
  {
    name: 'actividad',
    href: '/actividad',
    label: 'Actividad',
    icon: { ios: 'list.bullet', android: 'list', web: 'list' },
    iconFocused: { ios: 'list.bullet', android: 'list', web: 'list' },
  },
] as const;

type TabBarButtonProps = TabTriggerSlotProps & {
  label: string;
  icon: (typeof TABS)[number]['icon'];
  iconFocused: (typeof TABS)[number]['iconFocused'];
};

function TabBarButton({
  label,
  icon,
  iconFocused,
  isFocused,
  style,
  ...props
}: TabBarButtonProps) {
  const getStyle = (state: PressableStateCallbackType) => [
    styles.tabButton,
    typeof style === 'function' ? style(state) : style,
  ];

  return (
    <Pressable
      {...props}
      accessibilityRole="button"
      style={getStyle}
      hitSlop={4}>
      <View style={[styles.iconWrap, isFocused && styles.iconWrapFocused]}>
        <SymbolView
          name={isFocused ? iconFocused : icon}
          size={21}
          tintColor={isFocused ? Casa.colors.tealDark : Casa.colors.tabInactive}
        />
      </View>
      <Text
        numberOfLines={1}
        style={[styles.tabLabel, isFocused && styles.tabLabelFocused]}>
        {label}
      </Text>
    </Pressable>
  );
}

export default function AppTabs() {
  const insets = useSafeAreaInsets();

  return (
    <Tabs>
      <TabSlot style={styles.slot} />
      <TabList asChild>
        <View style={[styles.tabBarWrapper, { paddingBottom: insets.bottom + 10 }]}>
          <View style={styles.tabBar}>
            {TABS.map((tab) => (
              <TabTrigger key={tab.name} name={tab.name} href={tab.href} asChild>
                <TabBarButton
                  label={tab.label}
                  icon={tab.icon}
                  iconFocused={tab.iconFocused}
                />
              </TabTrigger>
            ))}
          </View>
        </View>
      </TabList>
    </Tabs>
  );
}

const styles = StyleSheet.create({
  slot: {
    flex: 1,
  },
  tabBarWrapper: {
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
    paddingHorizontal: 14,
  },
  tabBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: Casa.colors.surfaceStrong,
    borderRadius: Casa.radius.tabBar,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.9)',
    paddingHorizontal: 6,
    paddingVertical: 8,
    shadowColor: '#526180',
    shadowOpacity: 0.14,
    shadowRadius: 22,
    shadowOffset: { width: 0, height: 10 },
    elevation: 12,
  },
  tabButton: {
    flex: 1,
    alignItems: 'center',
    gap: 4,
  },
  iconWrap: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 12,
    paddingVertical: 4,
    borderRadius: 12,
  },
  iconWrapFocused: {
    backgroundColor: Casa.colors.tealSoft,
  },
  tabLabel: {
    fontSize: 10,
    lineHeight: 13,
    fontWeight: '600',
    color: Casa.colors.tabInactive,
  },
  tabLabelFocused: {
    color: Casa.colors.tealDark,
    fontWeight: '700',
  },
});
