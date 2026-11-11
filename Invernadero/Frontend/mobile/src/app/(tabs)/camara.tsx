import { MaterialCommunityIcons, MaterialIcons } from '@expo/vector-icons';
import { Image } from 'expo-image';
import * as ExpoImagePicker from 'expo-image-picker';
import { Modal, Pressable, ScrollView, Text, View } from 'react-native';
import { useState } from 'react';
import { Card } from '@/components/ui/Card';
import { Vacio } from '@/components/ui/Estados';
import { LiveFeed } from '@/components/LiveFeed';
import { colores } from '@/theme/tokens';
import {
  useActuador,
  useDispositivo,
  useRegarAhora,
  useSimuladorInvalidador,
} from '@/hooks/useTelemetria';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { escaneosService } from '@/services';
import { router } from 'expo-router';
import { relativo } from '../../../utils/formato';
import { hapticoExito, hapticoToque } from '../../../utils/haptico';
import { Select } from '@/components/ui/Select';
import type { EscaneoPlanta, EstadoSaludEscaneo } from '../../../types';
import { Boton } from '@/components/ui/Botones';
import { useColorScheme } from 'nativewind';

type Modo = 'vivo' | 'identificar';

export default function Camara() {
  useSimuladorInvalidador();
  const dispositivo = useDispositivo();
  const actuador = useActuador();
  const regar = useRegarAhora();
  const qc = useQueryClient();
  const online = dispositivo.data?.online ?? false;
  const { colorScheme } = useColorScheme();
  const tema = colorScheme === 'dark' ? colores.oscuro : colores.claro;
  const [modo, setModo] = useState<Modo>('vivo');
  const [ventilando, setVentilando] = useState(false);
  const [escaneoEnCurso, setEscaneoEnCurso] = useState(false);
  const [resultado, setResultado] = useState<EscaneoPlanta | null>(null);
  const [modalAsignar, setModalAsignar] = useState(false);

  const escaneos = useQuery({ queryKey: ['escaneos'], queryFn: escaneosService.listarEscaneos });

  async function identificar(desGaleria: boolean) {
    hapticoToque();
    let uri: string | null = null;
    try {
      const permiso = await ExpoImagePicker.requestMediaLibraryPermissionsAsync();
      if (desGaleria) {
        const r = await ExpoImagePicker.launchImageLibraryAsync({ quality: 0.8 });
        uri = (r.canceled ? null : r.assets[0]?.uri) ?? null;
      } else {
        const permisoCam = await ExpoImagePicker.requestCameraPermissionsAsync();
        if (!permisoCam.granted && !permiso.granted) {
          uri = null;
        } else {
          const r = await ExpoImagePicker.launchCameraAsync({ quality: 0.8 });
          uri = (r.canceled ? null : r.assets[0]?.uri) ?? null;
        }
      }
    } catch {
      uri = null; // en Expo Go web sin cámara
    }
    setEscaneoEnCurso(true);
    try {
      const r = await escaneosService.identificarPlanta(uri);
      setResultado(r);
      void qc.invalidateQueries({ queryKey: ['escaneos'] });
      hapticoExito();
    } finally {
      setEscaneoEnCurso(false);
    }
  }

  const saludColor: Record<EstadoSaludEscaneo, string> = {
  vigorosa_sana: tema.primaryContainer,
  observar: tema.tertiaryContainer,
  plaga_detectada: tema.errorContainer,
  };

  return (
    <View className="flex-1 bg-surface">
      {/* Segmented control */}
      <View className="mx-gutter-mobile flex-row rounded-xl bg-surface-container p-1">
        {(['vivo', 'identificar'] as Modo[]).map((m) => (
          <Pressable
            key={m}
            onPress={() => {
              hapticoToque();
              setModo(m);
            }}
            className={`min-h-[40px] flex-1 items-center justify-center rounded-lg py-2 ${modo === m ? 'bg-surface-container-lowest shadow-sm' : ''}`}
            accessibilityRole="button"
          >
            <Text className={`text-label-lg font-semibold ${modo === m ? 'text-primary' : 'text-on-surface-variant'}`}>
              {m === 'vivo' ? 'En vivo' : 'Identificar'}
            </Text>
          </Pressable>
        ))}
      </View>

      <ScrollView contentContainerStyle={{ paddingBottom: 96 }} showsVerticalScrollIndicator={false}>
        <View className="px-gutter-mobile pt-3">
          {modo === 'vivo' ? (
            <>
              {/* Envío del visor */}
              <LiveFeed online={online} />

              {/* Controles manuales bajo el visor */}
              <Card className="mt-2 gap-2">
                <Text className="text-title-sm text-on-surface" style={{ fontWeight: '600' }}>
                  Control manual remoto
                </Text>
                <View className="flex-row gap-2">
                  <Boton
                    texto="Regar 180 mL"
                    onPress={() =>
                      void regar
                        .mutateAsync({ plantaId: 'albahaca-1', volumenMl: 180, operador: 'Operador' })
                        .then(() => hapticoExito())
                        .catch(() => undefined)
                    }
                    deshabilitado={!online || regar.isPending}
                    className="flex-1"
                  />
                  <Boton
                    texto={ventilando ? 'Ventilando…' : 'Ventilar 10 s'}
                    onPress={() => {
                      if (ventilando) return; // sin doble envío
                      hapticoToque();
                      setVentilando(true);
                      void actuador
                        .mutateAsync({ tipo: 'ventilador', valor: true })
                        .then(() => {
                          return new Promise((res) => setTimeout(res, 10_000));
                        })
                        .then(() => actuador.mutateAsync({ tipo: 'ventilador', valor: false }))
                        .then(() => hapticoExito())
                        .catch(() => undefined)
                        .finally(() => setVentilando(false));
                    }}
                    deshabilitado={!online || ventilando}
                    variante="secundario"
                    className="flex-1"
                  />
                </View>
                {!online && (
                  <Text className="text-[11px] text-on-surface-variant">
                    RPi Offline: los controles manuales están deshabilitados hasta la reconexión.
                  </Text>
                )}
              </Card>
            </>
          ) : (
            <>
              {/* Modo identificar */}
              <Card className="flex-row items-center justify-between py-2.5">
                <View className="flex-row items-center gap-2">
                  <MaterialCommunityIcons name="brain" size={18} color={tema.primary} />
                  <Text className="text-label-md text-on-surface">Azure AI Vision v4.2</Text>
                </View>
                <View className="rounded-full bg-primary-container px-2 py-0.5">
                  <Text className="text-label-sm font-semibold text-on-primary-container">98.4% Precisión</Text>
                </View>
              </Card>

              {/* Escáner */}
              <View className="relative mt-2 h-72 overflow-hidden rounded-2xl bg-surface-container-highest">
                <Image
                  source={{ uri: resultado?.imagenUrl ?? 'https://lh3.googleusercontent.com/aida-public/AB6AXuCEEDiT32I33dn49coYdCDPASJuFq1S9endItkzyLqhs-dM7kQhWH_KtfOPQNxdrCwHioPWip-YmtI6l78XajbwB7qvuvqlt4JyKtgE3oamhfH_V_56lgftT1-D246Z3AYbBVvHUHRHiWl0Ao9Gbt25tMEffj0pLj3C00FsJ-AU5KfiD_zvxKkMrP1SgBonnLjjoGe-unTTUmKIsui69SpWM7TAXPtVcDytwMe7GZhBDFGJPrxQQb_8' }}
                  style={{ width: '100%', height: '100%' }}
                  contentFit="cover"
                />
                <View className="pointer-events-none absolute inset-0 items-center justify-center">
                  <View className="h-56 w-56 items-center justify-center">
                    <View className="absolute inset-0 rounded-2xl border-[3px] border-primary-fixed/80" />
                    {/* Línea de escaneo */}
                    {escaneoEnCurso && (
                      <View
                        className="h-0.5 w-44 rounded-full bg-primary-fixed"
                        style={{ shadowColor: tema.primaryFixed, shadowRadius: 12 }}
                      />
                    )}
                  </View>
                </View>

                <View className="absolute bottom-3 left-0 right-0 flex-row items-center justify-around px-4">
                  <Pressable
                    onPress={() => void identificar(true)}
                    className="items-center gap-1"
                    accessibilityRole="button"
                    accessibilityLabel="Elegir de la galería"
                  >
                    <View className="h-11 w-11 items-center justify-center rounded-full bg-white/25">
                      <MaterialCommunityIcons name="image-multiple" size={22} color={tema.surfaceBright} />
                    </View>
                    <Text className="text-label-sm text-white">Galería</Text>
                  </Pressable>
                  {/* Obturador */}
                  <Pressable
                    onPress={() => void identificar(false)}
                    className="items-center active:scale-90"
                    accessibilityRole="button"
                    accessibilityLabel="Capturar y escanear"
                  >
                    <View className="items-center justify-center rounded-full bg-primary-container p-1" style={{ width: 72, height: 72 }}>
                      <View className="h-[60px] w-[60px] items-center justify-center rounded-full bg-primary">
                        <MaterialCommunityIcons name="scanner" size={30} color={tema.onPrimary} />
                      </View>
                    </View>
                  </Pressable>
                  <View className="items-center gap-1">
                    <View className="h-11 w-11 items-center justify-center rounded-full bg-white/25">
                      <MaterialCommunityIcons name="flash-auto" size={22} color={tema.surfaceBright} />
                    </View>
                    <Text className="text-label-sm text-white">Flash</Text>
                  </View>
                </View>
              </View>

              {escaneoEnCurso && (
                <Card className="mt-2 items-center">
                  <Text className="text-body-md text-on-surface-variant">Analizando con Azure AI…</Text>
                </Card>
              )}

              {/* Resultado */}
              {resultado && !escaneoEnCurso && (
                <Card className="mt-2 gap-2">
                  <View className="flex-row items-start justify-between">
                    <View className="flex-row items-center gap-2" style={{ flex: 1 }}>
                      <Image source={{ uri: resultado.imagenUrl }} style={{ width: 56, height: 56, borderRadius: 12 }} />
                      <View className="min-w-0 flex-col" style={{ flex: 1 }}>
                        <Text className="text-label-sm font-semibold uppercase text-primary">Especie identificada</Text>
                        <Text className="text-title-lg text-on-surface">{resultado.especie}</Text>
                        <Text className="text-body-sm text-on-surface-variant" style={{ fontStyle: 'italic' }}>
                          {resultado.nombreCientifico}
                        </Text>
                      </View>
                    </View>
                    <View className="items-end gap-1">
                      <View className="rounded-full bg-primary-container px-2.5 py-1">
                        <Text className="text-label-sm font-semibold text-on-primary-container">
                          {resultado.confianzaPct.toFixed(1)}% Certeza
                        </Text>
                      </View>
                      <View className="rounded-full px-2 py-0.5" style={{ backgroundColor: saludColor[resultado.salud] }}>
                        <Text className="text-label-sm text-on-surface">{resultado.descripcionSalud}</Text>
                      </View>
                    </View>
                  </View>

                  {/* Condiciones óptimas */}
                  <Text className="text-title-sm text-on-surface" style={{ fontWeight: '600' }}>
                    Condiciones óptimas de cultivo
                  </Text>
                  <View className="flex-row gap-2">
                    <View className="flex-1 gap-1 rounded-xl bg-surface-container-low p-2">
                      <Text className="text-label-sm font-semibold text-secondary">Sustrato</Text>
                      <Text className="text-title-sm text-on-surface">{resultado.condicionesOptimas.humedadSustrato}</Text>
                    </View>
                    <View className="flex-1 gap-1 rounded-xl bg-surface-container-low p-2">
                      <Text className="text-label-sm font-semibold text-tertiary">Térmico</Text>
                      <Text className="text-title-sm text-on-surface">{resultado.condicionesOptimas.temperatura}</Text>
                    </View>
                  </View>
                  <View className="flex-row gap-2">
                    <View className="flex-1 gap-1 rounded-xl bg-surface-container-low p-2">
                      <Text className="text-label-sm font-semibold text-tertiary">Fotoperiodo</Text>
                      <Text className="text-title-sm text-on-surface">{resultado.condicionesOptimas.fotoperiodo}</Text>
                    </View>
                    <View className="flex-1 gap-1 rounded-xl bg-surface-container-low p-2">
                      <Text className="text-label-sm font-semibold text-secondary">Riego Smart</Text>
                      <Text className="text-title-sm text-on-surface">{resultado.condicionesOptimas.riegoSugerido}</Text>
                    </View>
                  </View>

                  <View className="rounded-xl bg-surface-container-low p-2">
                    <Text className="text-label-md font-semibold text-on-surface">Perfil Agronómico</Text>
                    <Text className="text-body-sm text-on-surface-variant">{resultado.perfilAgronomico}</Text>
                  </View>

                  {/* Acciones */}
                  <View className="gap-2 pt-1">
                    <Pressable
                      onPress={() => {
                        hapticoToque();
                        setModalAsignar(true);
                      }}
                      className="min-h-[48px] flex-row items-center justify-center gap-2 rounded-xl bg-primary py-3"
                      accessibilityRole="button"
                    >
                      <MaterialCommunityIcons name="sprout" size={20} color={tema.onPrimary} />
                      <Text className="text-label-lg font-semibold text-on-primary">Asignar a Slot del Invernadero</Text>
                    </Pressable>
                    <Pressable
                      onPress={() => router.push('/vivero-mapa')}
                      className="min-h-[44px] flex-row items-center justify-center gap-2 rounded-xl bg-surface-container py-2.5"
                      accessibilityRole="button"
                    >
                      <MaterialCommunityIcons name="map-marker-radius" size={18} color={tema.onSurface} />
                      <Text className="text-label-lg text-on-surface">Dónde conseguirla</Text>
                    </Pressable>
                  </View>
                </Card>
              )}

              {/* Historial de escaneos */}
              <View className="mt-3">
                <View className="flex-row items-center justify-between px-1">
                  <View className="flex-row items-center gap-1.5">
                    <MaterialIcons name="history" size={20} color={tema.primary} />
                    <Text className="text-title-md text-on-surface">Historial de Escaneos</Text>
                  </View>
                </View>
                <View className="mt-2 gap-2">
                  {(escaneos.data ?? []).slice(0, 5).map((s) => (
                    <Card key={s.id} className="flex-row items-center justify-between p-2.5">
                      <View className="min-w-0 flex-row items-center gap-2">
                        <Image source={{ uri: s.imagenUrl }} style={{ width: 48, height: 48, borderRadius: 12 }} />
                        <View className="min-w-0 flex-col" style={{ flex: 1 }}>
                          <Text className="text-title-sm text-on-surface" numberOfLines={1}>{s.especie}</Text>
                          <Text className="text-body-sm text-on-surface-variant" numberOfLines={1} style={{ fontStyle: 'italic' }}>
                            {s.nombreCientifico}
                          </Text>
                          <Text className="text-label-sm text-on-surface-variant">{relativo(s.fecha)}</Text>
                        </View>
                      </View>
                      <View className="items-end">
                        <View className="rounded-full bg-primary-container px-2 py-0.5">
                          <Text className="text-label-sm font-semibold text-on-primary-container">
                            {s.confianzaPct.toFixed(1)}%
                          </Text>
                        </View>
                        <Text className="mt-1 text-label-sm text-on-surface-variant">{s.sectorAsignado ?? 'Sin slot'}</Text>
                      </View>
                    </Card>
                  ))}
                  {(escaneos.data ?? []).length === 0 && <Vacio titulo="Sin escaneos aún" descripcion="Toma una foto para identificar tu primera planta." />}
                </View>
              </View>
            </>
          )}
        </View>
      </ScrollView>

      {/* Modal asignar a slot */}
      <Modal visible={modalAsignar} transparent animationType="fade" onRequestClose={() => setModalAsignar(false)}>
        <View className="flex-1 items-center justify-center bg-black/50 px-6">
          <View className="w-full max-w-[320px] rounded-2xl bg-surface-container-lowest p-4">
            <Text className="text-title-md text-on-surface">Asignar a slot</Text>
            <Text className="mt-1 text-body-sm text-on-surface-variant">
              Elige el sector del invernadero donde plantarás {resultado?.especie}
            </Text>
            <View className="mt-3">
              <Select
                valor="a1"
                opciones={[
                  { valor: 'a1', etiqueta: 'Sector A1 (slot libre)' },
                  { valor: 'b2', etiqueta: 'Sector B2' },
                  { valor: 'c1', etiqueta: 'Sector C1' },
                ]}
                onChange={(sector) => {
                  if (resultado) {
                    void escaneosService.asignarSector(resultado.id, sector.toUpperCase()).then(() => {
                      hapticoExito();
                      void escaneos.refetch();
                    });
                  }
                  setModalAsignar(false);
                }}
              />
            </View>
            <Pressable
              onPress={() => setModalAsignar(false)}
              className="mt-3 min-h-[44px] items-center justify-center rounded-xl bg-surface-container py-2.5"
              accessibilityRole="button"
            >
              <Text className="text-label-lg font-semibold text-on-surface">Ahora no</Text>
            </Pressable>
          </View>
        </View>
      </Modal>
    </View>
  );
}
