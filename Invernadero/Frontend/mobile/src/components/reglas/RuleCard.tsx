import { MaterialIcons } from '@expo/vector-icons';
import type { ComponentProps } from 'react';
import { Pressable, StyleSheet, Switch, Text, View } from 'react-native';
import { useColorScheme } from 'nativewind';
import { colores } from '@/theme/tokens';
import type { Categoria, Prioridad, Regla } from './datos';

type Icono = ComponentProps<typeof MaterialIcons>['name'];

interface Props {
  regla: Regla;
  onToggle: (activa: boolean) => void;
  /** Si no se pasan, el botón correspondiente no se muestra. */
  onEditar?: () => void;
  onEliminar?: () => void;
}

/** Tarjeta de regla SI/ENTONCES con switch y prioridad. */
export function RuleCard({ regla, onToggle, onEditar, onEliminar }: Props) {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const CATEGORIA: Record<Categoria, { icono: Icono; fondo: string; color: string }> = {
    riego: {
      icono: 'water-drop',
      fondo: tema.primaryContainer,
      color: tema.onPrimaryContainer,
    },
    clima: {
      icono: 'device-thermostat',
      fondo: tema.tertiaryContainer,
      color: tema.onTertiaryContainer,
    },
    iluminacion: {
      icono: 'light-mode',
      fondo: tema.secondaryContainer,
      color: tema.onSecondaryContainer,
    },
    seguridad: {
      icono: 'shield',
      fondo: tema.errorContainer,
      color: tema.onErrorContainer,
    },
  };

  const PRIORIDAD: Record<Prioridad, { texto: string; fondo: string; color: string }> = {
    alta: {
      texto: 'Prioridad Alta',
      fondo: tema.errorContainer,
      color: tema.onErrorContainer,
    },
    media: {
      texto: 'Prioridad Media',
      fondo: tema.surfaceContainerHigh,
      color: tema.onSurface,
    },
    baja: {
      texto: 'Prioridad Baja',
      fondo: tema.surfaceContainer,
      color: tema.onSurfaceVariant,
    },
  };

  const cat = CATEGORIA[regla.categoria];
  const pri = PRIORIDAD[regla.prioridad];

  return (
    <View style={[s.tarjeta, { backgroundColor: tema.surfaceContainerLowest }]}>
      <View style={s.encabezado}>
        <View style={s.contenidoEncabezado}>
          <View style={[s.icono, { backgroundColor: cat.fondo }]}>
            <MaterialIcons name={cat.icono} size={20} color={cat.color} />
          </View>

          <View style={s.informacion}>
            <Text
              style={[s.nombre, { color: tema.onSurface }]}
              numberOfLines={2}
            >
              {regla.nombre}
            </Text>

            <View style={s.detalles}>
              <Text style={[s.badge, { backgroundColor: pri.fondo, color: pri.color }]}>
                {pri.texto}
              </Text>

              {regla.bloqueada && (
                <Text style={[s.sub, { color: tema.onSurfaceVariant }]}>
                  🔒 Seguridad
                </Text>
              )}
            </View>
          </View>
        </View>

        <Switch
          style={s.interruptor}
          value={regla.activa}
          onValueChange={onToggle}
          disabled={regla.bloqueada}
          trackColor={{
            false: tema.surfaceContainerHighest,
            true: tema.primary,
          }}
        />
      </View>

      <View style={[s.condicion, { backgroundColor: tema.surfaceContainerLow }]}>
        <View style={s.fila}>
          <Text
            style={[
              s.etiqueta,
              { color: regla.categoria === 'seguridad' ? tema.error : tema.secondary },
            ]}
          >
            SI
          </Text>
          <Text style={[s.texto, { color: tema.onSurface }]}>
            {regla.si}
          </Text>
        </View>

        <View style={s.fila}>
          <Text style={[s.etiqueta, { color: tema.primary }]}>
            ENTONCES
          </Text>
          <Text style={[s.texto, { color: tema.onSurface, fontWeight: '500' }]}>
            {regla.entonces}
          </Text>
        </View>
      </View>

      <View style={s.fila}>
        <MaterialIcons name="schedule" size={16} color={cat.color} />
        <Text style={[s.sub, { color: tema.onSurfaceVariant, flexShrink: 1 }]}>
          {regla.ultimoDisparo
            ? `Último disparo: ${regla.ultimoDisparo}`
            : 'Sin disparos recientes'}
        </Text>
      </View>

      {(onEditar || onEliminar) && (
        <View style={s.acciones}>
          {onEditar && (
            <Pressable
              onPress={onEditar}
              accessibilityRole="button"
              accessibilityLabel={`Editar regla ${regla.nombre}`}
              style={[s.boton, { backgroundColor: tema.surfaceContainer }]}
            >
              <MaterialIcons name="edit" size={16} color={tema.onSurface} />
              <Text style={[s.botonTexto, { color: tema.onSurface }]}>Editar</Text>
            </Pressable>
          )}
          {onEliminar && (
            <Pressable
              onPress={onEliminar}
              accessibilityRole="button"
              accessibilityLabel={`Eliminar regla ${regla.nombre}`}
              style={[s.boton, { backgroundColor: tema.errorContainer }]}
            >
              <MaterialIcons name="delete-outline" size={16} color={tema.onErrorContainer} />
              <Text style={[s.botonTexto, { color: tema.onErrorContainer }]}>Eliminar</Text>
            </Pressable>
          )}
        </View>
      )}
    </View>
  );
}

const s = StyleSheet.create({
  tarjeta: {
    width: '100%',
    borderRadius: 12,
    padding: 16,
    gap: 8,
  },
  encabezado: {
    width: '100%',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  contenidoEncabezado: {
    flex: 1,
    minWidth: 0,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  informacion: {
    flex: 1,
    minWidth: 0,
    gap: 4,
  },
  interruptor: {
    flexShrink: 0,
    marginRight: -4,
  },
  fila: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  detalles: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: 6,
  },
  icono: {
    width: 36,
    height: 36,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
  },
  nombre: {
    fontSize: 15,
    fontWeight: '600',
  },
  badge: {
    borderRadius: 999,
    paddingHorizontal: 8,
    paddingVertical: 2,
    fontSize: 11,
    overflow: 'hidden',
  },
  condicion: {
    gap: 4,
    borderRadius: 12,
    padding: 12,
  },
  etiqueta: {
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 0.5,
    flexShrink: 0,
  },
  texto: {
    fontSize: 13,
    flex: 1,
    flexShrink: 1,
  },
  sub: {
    fontSize: 12,
  },
  acciones: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    gap: 8,
  },
  boton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  botonTexto: {
    fontSize: 13,
    fontWeight: '500',
  },
});
