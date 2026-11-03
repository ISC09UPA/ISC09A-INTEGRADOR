import { Pressable, Text, type ViewProps } from 'react-native';

type Variante = 'primario' | 'neutro' | 'contenedor' | 'exito' | 'advertencia' | 'error';

const FONDOS: Record<Variante, string> = {
  primario: 'bg-primary',
  neutro: 'bg-surface-container-lowest',
  contenedor: 'bg-surface-container',
  exito: 'bg-primary-container',
  advertencia: 'bg-tertiary-container',
  error: 'bg-error-container',
};

const TEXTOS: Record<Variante, string> = {
  primario: 'text-on-primary',
  neutro: 'text-on-surface-variant',
  contenedor: 'text-on-surface',
  exito: 'text-on-primary-container',
  advertencia: 'text-on-tertiary-container',
  error: 'text-on-error-container',
};

interface ChipProps extends ViewProps {
  variante?: Variante;
  texto: string;
  conteo?: number;
  activo?: boolean;
  onPress?: () => void;
}

export function Chip({ variante = 'neutro', texto, conteo, activo, onPress, className = '' }: ChipProps) {
  const v: Variante = activo ? 'primario' : variante;
  return (
    <Pressable
      onPress={onPress}
      disabled={!onPress}
      className={`flex-row items-center gap-1 rounded-full px-3 py-1.5 shadow-sm ${FONDOS[v]} min-h-[36px] ${className}`}
    >
      <Text className={`text-[12px] leading-4 font-medium ${TEXTOS[v]}`}>{texto}</Text>
      {conteo !== undefined && (
        <Text className={`rounded-full px-1.5 text-[11px] font-semibold ${TEXTOS[v]}`}>{conteo}</Text>
      )}
    </Pressable>
  );
}
