import { View, type ViewProps } from 'react-native';

interface Props extends ViewProps {
  elevada?: boolean;
  tono?: 'blanco' | 'bajo';
}

export function Card({ elevada = false, tono = 'blanco', className = '', children, ...resto }: Props) {
  const fondo = tono === 'blanco' ? 'bg-surface-container-lowest' : 'bg-surface-container-low';
  return (
    <View
      className={`rounded-2xl p-space-md ${fondo} ${elevada ? 'shadow-md shadow-black/10' : 'shadow-sm shadow-black/5'} ${className}`}
      {...resto}
    >
      {children}
    </View>
  );
}
