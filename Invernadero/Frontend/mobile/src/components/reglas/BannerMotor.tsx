
import { MaterialIcons } from '@expo/vector-icons';
import { Text, View } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';

/** Banner superior: motor de reglas en modo autónomo y cuántas están activas. */
export function BannerMotor({ activas, total }: { activas: number; total: number }) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  return (
    <View
      className="flex-row items-center gap-2 rounded-2xl p-3"
      style={{ backgroundColor: tema.primary }}
    >
      <View
        className="h-10 w-10 items-center justify-center rounded-xl"
        style={{ backgroundColor: 'rgba(255,255,255,0.2)' }}
      >
        <MaterialIcons name="psychology" size={22} color={tema.onPrimary} />
      </View>

      <View>
        <Text style={{ fontSize: 16, fontWeight: '600', color: tema.onPrimary }}>
          Motor de Reglas
        </Text>
        <Text style={{ fontSize: 12, color: tema.onPrimary, opacity: 0.9 }}>
          Modo Autónomo · {activas}/{total} activas
        </Text>
      </View>
    </View>
  );
}
