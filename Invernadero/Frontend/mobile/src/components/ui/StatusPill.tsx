import { Text, View } from 'react-native';

interface Props {
  online: boolean;
}

export function StatusPill({ online }: Props) {
  return (
    <View
      className={`flex-row items-center gap-1.5 rounded-full px-2.5 py-1 ${online ? 'bg-primary-container' : 'bg-error-container'}`}
      accessibilityLabel={online ? 'Raspberry Pi en línea' : 'Raspberry Pi desconectada'}
    >
      <View
        className={`h-2 w-2 rounded-full ${online ? 'bg-primary' : 'bg-error'}`}
        style={online ? { opacity: 0.9 } : undefined}
      />
      <Text className={`text-[11px] leading-[14px] font-medium ${online ? 'text-on-primary-container' : 'text-on-error-container'}`}>
        {online ? 'RPi Online' : 'RPi Offline'}
      </Text>
    </View>
  );
}
