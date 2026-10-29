import { SymbolView, type AndroidSymbol, type SFSymbol, type SymbolWeight } from 'expo-symbols';
import type { ColorValue, ViewProps } from 'react-native';
import { View } from 'react-native';

export type IconSource = {
  ios: SFSymbol;
  android?: AndroidSymbol;
  web?: AndroidSymbol;
};

export type IconProps = ViewProps & {
  source: IconSource;
  size?: number;
  color?: ColorValue;
  weight?: SymbolWeight;
  /** Rendered on platforms without a matching symbol. */
  fallback?: React.ReactNode;
};

/**
 * Cross-platform icon backed by SF Symbols on iOS and Material Symbols on
 * Android/web, with a neutral fallback dot when no symbol is available.
 */
export function Icon({ source, size = 20, color, weight = 'regular', fallback, ...rest }: IconProps) {
  return (
    <View {...rest} style={[{ width: size, height: size }, rest.style]}>
      <SymbolView
        name={{ ios: source.ios, android: source.android, web: source.web ?? source.android }}
        weight={weight}
        size={size}
        tintColor={color}
        fallback={
          fallback ?? (
            <View
              style={{
                width: size * 0.45,
                height: size * 0.45,
                borderRadius: size,
                backgroundColor: color ?? '#12263A',
              }}
            />
          )
        }
      />
    </View>
  );
}

/** Common icon set for the app. */
export const Icons = {
  home: { ios: 'house.fill', android: 'home' } satisfies IconSource,
  bulb: { ios: 'lightbulb.fill', android: 'lightbulb' } as IconSource,
  shield: { ios: 'shield.fill', android: 'shield' } as IconSource,
  camera: { ios: 'video.fill', android: 'videocam' } as IconSource,
  activity: { ios: 'chart.bar.fill', android: 'history' } as IconSource,
  thermometer: { ios: 'thermometer.medium', android: 'thermostat' } as IconSource,
  bolt: { ios: 'bolt.fill', android: 'bolt' } as IconSource,
  drop: { ios: 'drop.fill', android: 'water_drop' } as IconSource,
  lock: { ios: 'lock.fill', android: 'lock' } as IconSource,
  unlock: { ios: 'lock.open.fill', android: 'lock_open' } as IconSource,
  door: { ios: 'door.left.hand.open', android: 'door_open' } as IconSource,
  motion: { ios: 'figure.walk', android: 'directions_walk' } as IconSource,
  bell: { ios: 'bell.fill', android: 'notifications' } as IconSource,
  plus: { ios: 'plus', android: 'add' } as IconSource,
  check: { ios: 'checkmark.circle.fill', android: 'check_circle' } as IconSource,
  chevronRight: { ios: 'chevron.right', android: 'chevron_right' } as IconSource,
  chevronLeft: { ios: 'chevron.left', android: 'chevron_left' } as IconSource,
  send: { ios: 'paperplane.fill', android: 'send' } as IconSource,
  settings: { ios: 'gearshape.fill', android: 'settings' } as IconSource,
  power: { ios: 'power', android: 'power_settings_new' } as IconSource,
  sun: { ios: 'sun.max.fill', android: 'wb_sunny' } as IconSource,
  moon: { ios: 'moon.fill', android: 'dark_mode' } as IconSource,
  person: { ios: 'person.fill', android: 'person' } as IconSource,
  wifi: { ios: 'wifi', android: 'wifi' } as IconSource,
  pin: { ios: 'mappin.and.ellipse', android: 'pin_drop' } as IconSource,
  chat: { ios: 'bubble.left.fill', android: 'chat_bubble' } as IconSource,
  grid: { ios: 'square.grid.2x2.fill', android: 'grid_view' } as IconSource,
  info: { ios: 'info.circle.fill', android: 'info' } as IconSource,
} as const;
