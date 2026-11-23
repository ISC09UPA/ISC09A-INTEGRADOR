
import { Text, View } from 'react-native';
import { useTema } from '@/hooks/useTema';

interface Props {
  online: boolean;
}

export function StatusPill({ online }: Props) {
  const tema = useTema();

  return (
    <View
      className="flex-row items-center gap-1.5 rounded-full px-2.5 py-1"
      style={{
        backgroundColor: online ? tema.primaryContainer : tema.errorContainer,
      }}
      accessibilityLabel={
        online ? 'Raspberry Pi en línea' : 'Raspberry Pi desconectada'
      }
    >
      <View
        className="h-2 w-2 rounded-full"
        style={{
          backgroundColor: online ? tema.primary : tema.error,
          opacity: online ? 0.9 : 1,
        }}
      />

      <Text
        className="text-[11px] leading-[14px] font-medium"
        style={{
          color: online ? tema.onPrimaryContainer : tema.onErrorContainer,
        }}
      >
        {online ? 'RPi Online' : 'RPi Offline'}
      </Text>
    </View>
  );
}
