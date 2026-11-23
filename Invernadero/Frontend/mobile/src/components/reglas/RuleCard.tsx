
import { MaterialIcons } from '@expo/vector-icons';
import type { ComponentProps } from 'react';

import {
  Pressable,
  StyleSheet,
  Switch,
  Text,
  View,
} from 'react-native';

import type { Regla } from '../../../types';

import { useConfiguracion } from '@/context/ConfiguracionContext';
import { useTema } from '@/hooks/useTema';

import {
  formatearFecha,
  formatearHora,
} from '../../../utils/formatoFechaHora';

type Icono = ComponentProps<typeof MaterialIcons>['name'];

interface Props {
  regla: Regla;
  soloLectura: boolean;
  onToggle: (activa: boolean) => void;
  onEditar?: () => void;
  onEliminar?: () => void;
}

const SENSORES: Record<string, string> = {
  humedadSuelo: 'Humedad sustrato',
  temperatura: 'Temperatura',
  luz: 'Luz',
  nivelDeposito: 'Nivel tanque',
  horario: 'Horario',
};

const ACTUADORES: Record<string, string> = {
  bomba: 'Bomba GPIO 17',
  ventilador: 'Ventilador GPIO 27',
  leds: 'Luces LED GPIO 22',
  valvulaLlenado: 'Válvula de llenado',
};

function textoCondicion(regla: Regla): string {
  const {
    sensor,
    operador,
    umbral,
    duranteMin,
  } = regla.si;

  const simbolo: Record<string, string> = {
    '<': '<',
    '>': '>',
    '<=': '≤',
    '>=': '≥',
    entre: 'entre',
  };

  const unidad =
    sensor === 'temperatura'
      ? ' °C'
      : sensor === 'humedadSuelo' ||
          sensor === 'nivelDeposito'
        ? '%'
        : sensor === 'luz'
          ? ' lx'
          : '';

  const duracion = duranteMin
    ? ` durante ${duranteMin} min`
    : '';

  return `${
    SENSORES[sensor] ?? sensor
  } ${simbolo[operador] ?? operador} ${umbral}${unidad}${duracion}`;
}

function textoAccion(regla: Regla): string {
  const {
    actuador,
    accion,
    volumenMl,
    hastaValor,
  } = regla.entonces;

  const nombre = ACTUADORES[actuador] ?? actuador;

  switch (accion) {
    case 'regar_volumen':
      return `Activar ${nombre} (${
        volumenMl ?? 180
      } mL${hastaValor ? ` hasta ${hastaValor}%` : ''})`;

    case 'encender':
      return `Encender ${nombre}`;

    case 'apagar':
      return `Apagar ${nombre}`;

    case 'bloquear':
      return `Bloquear ${nombre} y generar alerta`;

    default:
      return `Ejecutar acción en ${nombre}`;
  }
}

export function RuleCard({
  regla,
  soloLectura,
  onToggle,
  onEditar,
  onEliminar,
}: Props) {
  const tema = useTema();
  const { config } = useConfiguracion();

  const CATEGORIA: Record<
    string,
    {
      icono: Icono;
      fondo: string;
      color: string;
    }
  > = {
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

  const PRIORIDAD: Record<
    string,
    {
      texto: string;
      fondo: string;
      color: string;
    }
  > = {
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

  const bloqueada =
    regla.bloqueadaPorSeguridad === true;

  return (
    <View
      style={[
        s.tarjeta,
        {
          backgroundColor: tema.surfaceContainerLowest,
        },
      ]}
    >
      <View style={s.encabezado}>
        <View style={s.contenidoEncabezado}>
          <View
            style={[
              s.icono,
              {
                backgroundColor: cat.fondo,
              },
            ]}
          >
            <MaterialIcons
              name={cat.icono}
              size={20}
              color={cat.color}
            />
          </View>

          <View style={s.informacion}>
            <Text
              style={[
                s.nombre,
                {
                  color: tema.onSurface,
                },
              ]}
              numberOfLines={2}
            >
              {regla.nombre}
            </Text>

            <View style={s.detalles}>
              <Text
                style={[
                  s.badge,
                  {
                    backgroundColor: pri.fondo,
                    color: pri.color,
                  },
                ]}
              >
                {pri.texto}
              </Text>

              {bloqueada && (
                <Text
                  style={[
                    s.sub,
                    {
                      color: tema.onSurfaceVariant,
                    },
                  ]}
                >
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
          disabled={bloqueada || soloLectura}
          trackColor={{
            false: tema.surfaceContainerHighest,
            true: tema.primary,
          }}
        />
      </View>

      <View
        style={[
          s.condicion,
          {
            backgroundColor: tema.surfaceContainerLow,
          },
        ]}
      >
        <View style={s.fila}>
          <Text
            style={[
              s.etiqueta,
              {
                color:
                  regla.categoria === 'seguridad'
                    ? tema.error
                    : tema.secondary,
              },
            ]}
          >
            SI
          </Text>

          <Text
            style={[
              s.texto,
              {
                color: tema.onSurface,
              },
            ]}
          >
            {textoCondicion(regla)}
          </Text>
        </View>

        <View style={s.fila}>
          <Text
            style={[
              s.etiqueta,
              {
                color: tema.primary,
              },
            ]}
          >
            ENTONCES
          </Text>

          <Text
            style={[
              s.texto,
              {
                color: tema.onSurface,
                fontWeight: '500',
              },
            ]}
          >
            {textoAccion(regla)}
          </Text>
        </View>
      </View>

      <View style={s.fila}>
        <MaterialIcons
          name="schedule"
          size={16}
          color={cat.color}
        />

        <Text
          style={[
            s.sub,
            {
              color: tema.onSurfaceVariant,
              flexShrink: 1,
            },
          ]}
        >
          {regla.ultimaEjecucion
            ? `Última ejecución: ${formatearFecha(
                regla.ultimaEjecucion,
                config.formatoFecha,
              )} ${formatearHora(
                regla.ultimaEjecucion,
                config.formatoHora,
              )}`
            : 'Sin ejecuciones recientes'}
        </Text>
      </View>

      {(onEditar || onEliminar) && (
        <View style={s.acciones}>
          {onEditar && (
            <Pressable
              onPress={onEditar}
              accessibilityRole="button"
              accessibilityLabel={`Editar regla ${regla.nombre}`}
              style={[
                s.boton,
                {
                  backgroundColor: tema.surfaceContainer,
                },
              ]}
            >
              <MaterialIcons
                name="edit"
                size={16}
                color={tema.onSurface}
              />

              <Text
                style={[
                  s.botonTexto,
                  {
                    color: tema.onSurface,
                  },
                ]}
              >
                Editar
              </Text>
            </Pressable>
          )}

          {onEliminar && (
            <Pressable
              onPress={onEliminar}
              accessibilityRole="button"
              accessibilityLabel={`Eliminar regla ${regla.nombre}`}
              style={[
                s.boton,
                {
                  backgroundColor: tema.errorContainer,
                },
              ]}
            >
              <MaterialIcons
                name="delete-outline"
                size={16}
                color={tema.onErrorContainer}
              />

              <Text
                style={[
                  s.botonTexto,
                  {
                    color: tema.onErrorContainer,
                  },
                ]}
              >
                Eliminar
              </Text>
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
