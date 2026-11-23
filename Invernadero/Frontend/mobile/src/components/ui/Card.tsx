import { View, type ViewProps } from 'react-native';
import { useTema } from '@/hooks/useTema';

interface Props extends ViewProps {
  elevada?: boolean;
  tono?: 'blanco' | 'bajo';
  className?: string;
}

export function Card({
  elevada = false,
  tono = 'blanco',
  className = '',
  children,
  style,
  ...resto
}: Props) {
  const tema = useTema();

  const fondo =
    tono === 'blanco'
      ? tema.surfaceContainerLowest
      : tema.surfaceContainerLow;

  return (
    <View
      className={`rounded-2xl p-space-md ${className}`}
      style={[
        { backgroundColor: fondo },
        elevada
          ? {
              shadowColor: '#000',
              shadowOpacity: 0.1,
              shadowRadius: 6,
              elevation: 4,
            }
          : {
              shadowColor: '#000',
              shadowOpacity: 0.05,
              shadowRadius: 3,
              elevation: 2,
            },
        style,
      ]}
      {...resto}
    >
      {children}
    </View>
  );
}