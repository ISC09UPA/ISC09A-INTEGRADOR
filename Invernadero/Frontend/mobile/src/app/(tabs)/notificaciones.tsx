import { useMemo, useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import { MaterialIcons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import type { TipoNotificacion } from '../../../types';
import { colores } from '@/theme/tokens';
import { notificacionesService } from '@/services';
import { Confirmar } from '@/components/Confirmar';
import { Vacio } from '@/components/ui/Estados';
import { useSesion } from '../../../store/useSesionStore';
import { hapticoExito, hapticoToque } from '../../../utils/haptico';
import { relativo } from '../../../utils/formato';
import { useColorScheme } from 'nativewind';

export default function Notificaciones() {
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;

  const ICONO_TIPO: Record<
    TipoNotificacion,
    { icono: string; fondo: string; color: string }
  > = {
    deposito_vacio: {
      icono: 'warning',
      fondo: tema.errorContainer,
      color: tema.onErrorContainer,
    },
    estres_hidrico: {
      icono: 'water-drop',
      fondo: tema.secondaryContainer,
      color: tema.onSecondaryContainer,
    },
    enfermedad_detectada: {
      icono: 'bug-report',
      fondo: tema.tertiaryContainer,
      color: tema.onTertiaryContainer,
    },
    riego_realizado: {
      icono: 'check-circle',
      fondo: tema.primaryContainer,
      color: tema.onPrimaryContainer,
    },
    dispositivo_desconectado: {
      icono: 'cloud-off',
      fondo: tema.surfaceContainerHigh,
      color: tema.onSurfaceVariant,
    },
  };

  const { usuario } = useSesion();
  const consultas = useQueryClient();

  const iniciales = (usuario?.nombre ?? 'US').slice(0, 2).toUpperCase();

  const lista = useQuery({
    queryKey: ['notificaciones'],
    queryFn: notificacionesService.listarNotificaciones,
  });

  const [permiso, setPermiso] = useState<notificacionesService.EstadoPermisos | null>(null);
  const [probarVisible, setProbarVisible] = useState(false);

  const noLeidas = useMemo(
    () => (lista.data ?? []).filter((n) => !n.leida).length,
    [lista.data]
  );

  function pedirPermiso() {
    void notificacionesService.solicitarPermisos().then((estado) => {
      setPermiso(estado);
      void notificacionesService.inicializarCanal();

      if (estado === 'granted') {
        hapticoExito();
      }

      void consultas.invalidateQueries({ queryKey: ['notificaciones'] });
    });
  }

  const estatus: notificacionesService.EstadoPermisos =
    permiso ?? 'undetermined';

  return (
    <View className="flex-1 bg-surface">
      {/* Encabezado */}
      <View className="h-16 flex-row items-center justify-between px-gutter-mobile">
        <View className="flex-row items-center gap-2">
          <Pressable
            onPress={() => router.back()}
            accessibilityRole="button"
            accessibilityLabel="Volver"
            className="h-10 w-10 items-center justify-center rounded-full bg-surface-container-low"
          >
            <MaterialIcons
              name="arrow-back"
              size={22}
              color={tema.onSurface}
            />
          </Pressable>

          <View>
            <Text className="text-headline-sm font-bold text-on-surface">
              Notificaciones
            </Text>

            <Text className="text-label-sm text-on-surface-variant">
              {lista.data
                ? `${noLeidas} sin leer de ${lista.data.length}`
                : 'Sincronizando…'}
            </Text>
          </View>
        </View>

        <Text className="text-[13px] font-semibold text-on-primary">
          {iniciales}
        </Text>
      </View>

      <ScrollView
        contentContainerStyle={{ paddingBottom: 24 }}
        showsVerticalScrollIndicator={false}
      >
        {/* Estado de permisos */}
        <View className="px-gutter-mobile pb-2">
          <View
            className={`rounded-xl p-3 ${
              estatus === 'granted'
                ? 'bg-primary-container'
                : 'bg-surface-container-lowest shadow-sm'
            }`}
          >
            <View className="flex-row items-center gap-2">
              <MaterialIcons
                name={
                  estatus === 'granted'
                    ? 'notifications-active'
                    : 'notifications-off'
                }
                size={22}
                color={
                  estatus === 'granted'
                    ? tema.onPrimaryContainer
                    : tema.onSurfaceVariant
                }
              />

              <View className="flex-1">
                <Text
                  className={`text-title-sm font-semibold ${
                    estatus === 'granted'
                      ? 'text-on-primary-container'
                      : 'text-on-surface'
                  }`}
                >
                  {estatus === 'granted'
                    ? 'Permisos concedidos'
                    : 'Permisos pendientes'}
                </Text>

                <Text
                  className={`text-body-sm ${
                    estatus === 'granted'
                      ? 'text-on-primary-container'
                      : 'text-on-surface-variant'
                  }`}
                >
                  {estatus === 'granted'
                    ? 'Recibirás avisos demo: depósito vacío, estrés hídrico, enfermedad, riego y desconexión.'
                    : 'Activa los permisos para recibir las notificaciones locales de demostración en este dispositivo.'}
                </Text>
              </View>

              {estatus !== 'granted' && (
                <Pressable
                  onPress={pedirPermiso}
                  className="min-h-[44px] justify-center rounded-xl bg-primary px-3 py-2.5"
                  accessibilityRole="button"
                  accessibilityLabel="Activar permisos de notificación"
                >
                  <Text className="text-label-md font-semibold text-on-primary">
                    Activar
                  </Text>
                </Pressable>
              )}
            </View>
          </View>
        </View>

        {/* Acciones demo */}
        <View className="flex-row gap-2 px-gutter-mobile pb-2">
          <Pressable
            onPress={() => {
              hapticoToque();
              setProbarVisible(true);
            }}
            className="min-h-[44px] flex-1 items-center justify-center rounded-xl bg-secondary py-2.5"
            accessibilityRole="button"
            accessibilityLabel="Probar notificación local"
          >
            <Text className="text-label-md font-semibold text-on-secondary">
              Probar demo
            </Text>
          </Pressable>

          <Pressable
            onPress={() => {
              hapticoToque();
              notificacionesService.marcarTodasLeidas();
              void consultas.invalidateQueries({
                queryKey: ['notificaciones'],
              });
            }}
            className="min-h-[44px] flex-1 items-center justify-center rounded-xl bg-surface-container py-2.5"
            accessibilityRole="button"
            accessibilityLabel="Marcar todas como leídas"
          >
            <Text className="text-label-md font-semibold text-on-surface">
              Marcar leídas
            </Text>
          </Pressable>
        </View>

        {/* Lista */}
        <View className="gap-2 px-gutter-mobile">
          {lista.isLoading ? (
            <Vacio
              titulo="Cargando demo…"
              descripcion="Sincronizando historial de notificaciones."
            />
          ) : (lista.data ?? []).length === 0 ? (
            <Vacio
              titulo="Sin notificaciones"
              descripcion="Cuando el invernadero dispare eventos verás los avisos aquí."
            />
          ) : (
            lista.data?.map((n) => {
              const meta = ICONO_TIPO[n.tipo];
              const semilla = [
                'n-seed-1',
                'n-seed-2',
                'n-seed-3',
              ].includes(n.id);

              return (
                <Pressable
                  key={n.id}
                  onPress={() => {
                    hapticoToque();
                    notificacionesService.marcarLeida(n.id);
                    void consultas.invalidateQueries({
                      queryKey: ['notificaciones'],
                    });
                  }}
                  className={`flex-row items-start gap-2 rounded-xl bg-surface-container-lowest p-3 shadow-sm ${
                    n.leida ? '' : 'border-l-4 border-l-primary'
                  }`}
                  accessibilityRole="button"
                  accessibilityLabel={`${n.titulo}: ${n.cuerpo}`}
                >
                  <View
                    className="h-9 w-9 items-center justify-center rounded-xl"
                    style={{ backgroundColor: meta.fondo }}
                  >
                    <MaterialIcons
                      name={meta.icono as never}
                      size={18}
                      color={meta.color}
                    />
                  </View>

                  <View className="flex-1">
                    <Text className="text-title-sm text-on-surface">
                      {semilla ? n.titulo : `${n.titulo} (demo)`}
                    </Text>

                    <Text className="text-body-sm text-on-surface-variant">
                      {n.cuerpo}
                    </Text>

                    <Text className="mt-0.5 text-label-sm text-on-surface-variant">
                      {relativo(n.fecha)}
                    </Text>
                  </View>

                  {!n.leida && (
                    <View className="mt-1 h-2 w-2 rounded-full bg-primary" />
                  )}
                </Pressable>
              );
            })
          )}
        </View>
      </ScrollView>

      {/* Confirmación de notificación demo */}
      <Confirmar
        visible={probarVisible}
        titulo="Probar notificación"
        descripcion="Se enviará una notificación local de demostración (riego realizado) al sistema."
        textoConfirmar="Enviar"
        onCancelar={() => setProbarVisible(false)}
        onConfirmar={() => {
          setProbarVisible(false);

          void notificacionesService.enviarLocal(
            'riego_realizado',
            'Riego realizado (demo)',
            'Bomba GPIO 17 suministró 180 mL en Albahaca #1 (notificación de prueba).'
          );

          hapticoExito();

          void consultas.invalidateQueries({
            queryKey: ['notificaciones'],
          });
        }}
      />
    </View>
  );
}