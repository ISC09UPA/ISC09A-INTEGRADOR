import { MaterialIcons } from '@expo/vector-icons';
import type { ComponentProps } from 'react';
import { StyleSheet, Switch, Text, View } from 'react-native';
import { colores } from '@/theme/tokens';
import type { Categoria, Prioridad, Regla } from './datos';

type Icono = ComponentProps<typeof MaterialIcons>['name'];

const CATEGORIA: Record<Categoria, { icono: Icono; fondo: string; color: string }> = {
  riego: { icono: 'water-drop', fondo: colores.primaryContainer, color: colores.onPrimaryContainer },
  clima: { icono: 'device-thermostat', fondo: colores.tertiaryContainer, color: colores.onTertiaryContainer },
  iluminacion: { icono: 'light-mode', fondo: colores.secondaryContainer, color: colores.onSecondaryContainer },
  seguridad: { icono: 'shield', fondo: colores.errorContainer, color: colores.onErrorContainer },
};

const PRIORIDAD: Record<Prioridad, { texto: string; fondo: string; color: string }> = {
  alta: { texto: 'Prioridad Alta', fondo: colores.errorContainer, color: colores.onErrorContainer },
  media: { texto: 'Prioridad Media', fondo: colores.surfaceContainerHigh, color: colores.onSurface },
  baja: { texto: 'Prioridad Baja', fondo: colores.surfaceContainer, color: colores.onSurfaceVariant },
};

interface Props {
  regla: Regla;
  onToggle: (activa: boolean) => void;
}

/** Tarjeta de regla SI/ENTONCES con switch y prioridad. */
export function RuleCard({ regla, onToggle }: Props) {
  const cat = CATEGORIA[regla.categoria];
  const pri = PRIORIDAD[regla.prioridad];
  return (
    <View style={s.tarjeta}>
      <View style={s.encabezado}>
        <View style={s.fila}>
          <View style={[s.icono, { backgroundColor: cat.fondo }]}>
            <MaterialIcons name={cat.icono} size={20} color={cat.color} />
          </View>
          <View style={{ flex: 1 }}>
            <Text style={s.nombre}>{regla.nombre}</Text>
            <View style={s.fila}>
              <Text style={[s.badge, { backgroundColor: pri.fondo, color: pri.color }]}>{pri.texto}</Text>
              {regla.bloqueada && <Text style={s.sub}>🔒 Regla de seguridad</Text>}
            </View>
          </View>
        </View>
        <Switch
          value={regla.activa}
          onValueChange={onToggle}
          disabled={regla.bloqueada}
          trackColor={{ false: colores.surfaceContainerHighest, true: colores.primary }}
        />
      </View>

      <View style={s.condicion}>
        <View style={s.fila}>
          <Text style={[s.etiqueta, { color: regla.categoria === 'seguridad' ? colores.error : colores.secondary }]}>SI</Text>
          <Text style={[s.texto, { flex: 1 }]}>{regla.si}</Text>
        </View>
        <View style={s.fila}>
          <Text style={[s.etiqueta, { color: colores.primary }]}>ENTONCES</Text>
          <Text style={[s.texto, { flex: 1, fontWeight: '500' }]}>{regla.entonces}</Text>
        </View>
      </View>

      <View style={s.fila}>
        <MaterialIcons name="schedule" size={16} color={cat.color} />
        <Text style={s.sub}>{regla.ultimoDisparo ? `Último disparo: ${regla.ultimoDisparo}` : 'Sin disparos recientes'}</Text>
      </View>
    </View>
  );
}

const s = StyleSheet.create({
  tarjeta: { backgroundColor: colores.surfaceContainerLowest, borderRadius: 12, padding: 16, gap: 8 },
  encabezado: { flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', gap: 8 },
  fila: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  icono: { width: 36, height: 36, borderRadius: 12, alignItems: 'center', justifyContent: 'center' },
  nombre: { fontSize: 15, fontWeight: '600', color: colores.onSurface },
  badge: { borderRadius: 999, paddingHorizontal: 8, paddingVertical: 2, fontSize: 11, overflow: 'hidden' },
  condicion: { gap: 4, borderRadius: 12, padding: 12, backgroundColor: colores.surfaceContainerLow },
  etiqueta: { fontSize: 11, fontWeight: '700', letterSpacing: 0.5 },
  texto: { fontSize: 13, color: colores.onSurface },
  sub: { fontSize: 12, color: colores.onSurfaceVariant },
});
