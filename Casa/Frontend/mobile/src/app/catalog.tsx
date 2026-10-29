import { router } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, useWindowDimensions, View } from 'react-native';

import {
  AppHeader,
  AppText,
  Avatar,
  AvatarGroup,
  Brand,
  Button,
  CameraCard,
  ChatBubble,
  ChatInput,
  DeviceCard,
  EventItem,
  Icon,
  IconButton,
  Icons,
  InfoRow,
  MapPreview,
  Metric,
  QuickAction,
  Screen,
  Section,
  Segmented,
  SettingsRow,
  StatusBanner,
  Surface,
  TAB_CONFIG,
  TextField,
  Toggle,
} from '@/components';
import { Palette, Radii, Spacing } from '@/theme';

/** Swatches shown in the palette section. */
const SWATCHES = [
  { name: 'Accent', color: Palette.accent },
  { name: 'Ink', color: Palette.ink },
  { name: 'Ink soft', color: Palette.inkSoft },
  { name: 'Success', color: Palette.success },
  { name: 'Warning', color: Palette.warning },
  { name: 'Danger', color: Palette.danger },
];

export default function CatalogScreen() {
  const { width } = useWindowDimensions();
  const columns = width >= 700 ? 2 : 1;
  const gap = Spacing.two;
  const contentWidth = Math.min(width, 800) - Spacing.three * 2;
  const halfWidth = (contentWidth - gap) / 2;
  const colWidth = (contentWidth - gap * (columns - 1)) / columns;

  const [toggleOn, setToggleOn] = useState(true);
  const [segment, setSegment] = useState('a');
  const [text, setText] = useState('');
  const [chatText, setChatText] = useState('');

  return (
    <Screen includeBottomInset>
      <AppHeader
        eyebrow="Diseño"
        title="Catálogo"
        subtitle="Componentes reutilizables de Casa"
        right={<IconButton label="Volver" icon={Icons.chevronLeft} onPress={() => router.canGoBack() ? router.back() : router.replace('/')} />}
      />

      <StatusBanner
        tone="neutral"
        icon={Icons.info}
        title="Contenido de ejemplo"
        description="Las tarjetas usan datos de muestra del mockup; la app real los conectará al backend."
      />

      <Section title="Marca">
        <Surface>
          <Brand size={44} />
        </Surface>
      </Section>

      <Section title="Paleta">
        <Surface>
          <View style={styles.wrapRow}>
            {SWATCHES.map((swatch) => (
              <View key={swatch.name} style={styles.swatch}>
                <View style={[styles.swatchColor, { backgroundColor: swatch.color }]} />
                <AppText variant="caption" color="inkSoft">
                  {swatch.name}
                </AppText>
              </View>
            ))}
          </View>
        </Surface>
      </Section>

      <Section title="Tipografía">
        <Surface>
          <AppText variant="displayLarge">Display</AppText>
          <AppText variant="display">Título de pantalla</AppText>
          <AppText variant="titleLarge">titleLarge · Manrope</AppText>
          <AppText variant="title">title · Manrope semibold</AppText>
          <AppText variant="headline">headline · Inter semibold</AppText>
          <AppText variant="body">body · Inter regular, texto corrido para descripciones.</AppText>
          <AppText variant="bodyMedium">bodyMedium · inter medium</AppText>
          <AppText variant="footnote" color="inkMuted">
            footnote · metadatos y horarios
          </AppText>
          <AppText variant="caption" color="inkMuted">
            CAPTION · ETIQUETAS
          </AppText>
        </Surface>
      </Section>

      <Section title="Botones">
        <View style={[styles.wrapRow, { gap }]}>
          <Button label="Primario" icon={Icons.check} style={{ width: halfWidth }} />
          <Button label="Secundario" variant="secondary" style={{ width: halfWidth }} />
          <Button label="Peligro" variant="danger" icon={Icons.unlock} style={{ width: halfWidth }} />
          <Button label="Fantasma" variant="ghost" iconRight={Icons.chevronRight} style={{ width: halfWidth }} />
          <Button label="Cargando" loading style={{ width: halfWidth }} />
          <Button label="Deshabilitado" disabled style={{ width: halfWidth }} />
        </View>
        <View style={styles.inlineRow}>
          <Button label="Pequeño" size="sm" variant="secondary" />
          <Button label="Mediano" size="md" />
          <Button label="Grande" size="lg" variant="secondary" />
          <IconButton label="Añadir" icon={Icons.plus} />
        </View>
      </Section>

      <Section title="Iconos">
        <Surface>
          <View style={styles.wrapRow}>
            {Object.entries(Icons).map(([name, source]) => (
              <View key={name} style={styles.swatch}>
                <Icon source={source} size={22} color={Palette.inkSoft} />
                <AppText variant="caption" color="inkMuted" numberOfLines={1}>
                  {name}
                </AppText>
              </View>
            ))}
          </View>
        </Surface>
      </Section>

      <Section title="Superficies">
        <View style={[styles.wrapRow, { gap }]}>
          <Surface tone="glass" style={{ width: halfWidth }}>
            <AppText variant="callout">Glass</AppText>
            <AppText variant="footnote" color="inkMuted">
              translúcida con blur
            </AppText>
          </Surface>
          <Surface tone="strong" style={{ width: halfWidth }}>
            <AppText variant="callout">Strong</AppText>
            <AppText variant="footnote" color="inkMuted">
              más opaca
            </AppText>
          </Surface>
          <Surface tone="subtle" style={{ width: halfWidth }}>
            <AppText variant="callout">Subtle</AppText>
            <AppText variant="footnote" color="inkMuted">
              capa tenue
            </AppText>
          </Surface>
        </View>
      </Section>

      <Section title="Banners de estado">
        <StatusBanner tone="accent" icon={Icons.info} title="Informativo" description="Tono accent" />
        <StatusBanner tone="success" icon={Icons.check} title="Correcto" description="Tono success" />
        <StatusBanner tone="warning" icon={Icons.bell} title="Aviso" description="Tono warning" />
        <StatusBanner tone="danger" icon={Icons.bell} title="Alerta" description="Tono danger" />
        <StatusBanner
          tone="accent"
          icon={Icons.bolt}
          title="Con acción"
          description="Banner con botón"
          action={<Button label="Acción" size="sm" />}
        />
      </Section>

      <Section title="Métricas">
        <View style={[styles.wrapRow, { gap }]}>
          <Metric style={{ width: halfWidth }} label="Salón" value="22.5" unit="°C" tone="accent" icon={Icons.thermometer} caption="Confortable" />
          <Metric style={{ width: halfWidth }} label="Consumo" value="1.2" unit="kW" tone="warning" icon={Icons.bolt} caption="−8% vs ayer" />
          <Metric style={{ width: halfWidth }} label="Humedad" value="48" unit="%" tone="success" icon={Icons.drop} />
          <Metric style={{ width: halfWidth }} label="Calidad aire" value="Buena" tone="neutral" icon={Icons.home} />
        </View>
      </Section>

      <Section title="Filas de información">
        <Surface>
          <InfoRow icon={Icons.wifi} tone="success" label="Router" value="En línea" />
          <InfoRow icon={Icons.door} tone="warning" label="Puerta trasera" description="Abierta hace 5 min" showChevron onPress={() => {}} />
          <InfoRow icon={Icons.motion} label="Movimiento" valueNode={<Toggle value={toggleOn} onValueChange={setToggleOn} label="Movimiento" />} />
        </Surface>
      </Section>

      <Section title="Eventos">
        <Surface padding={Spacing.two}>
          <EventItem icon={Icons.unlock} tone="warning" title="Alarma desarmada" description="Ana desde el móvil" time="12:03" />
          <EventItem icon={Icons.bulb} tone="accent" title="Lámpara encendida" time="11:58" />
          <EventItem icon={Icons.lock} tone="success" title="Alarma armada" time="23:15" />
        </Surface>
      </Section>

      <Section title="Ajustes">
        <Surface>
          <SettingsRow
            icon={Icons.bell}
            label="Notificaciones"
            description="Alertas de seguridad y dispositivos"
            value={<Toggle value={toggleOn} onValueChange={setToggleOn} label="Notificaciones" />}
          />
          <SettingsRow icon={Icons.person} label="Miembros" description="3 personas" onPress={() => {}} />
          <SettingsRow icon={Icons.settings} label="Preferencias" onPress={() => {}} />
        </Surface>
      </Section>

      <Section title="Controles">
        <Surface>
          <View style={styles.controlGroup}>
            <AppText variant="caption" color="inkMuted">
              TOGGLE
            </AppText>
            <View style={styles.inlineRow}>
              <Toggle value={toggleOn} onValueChange={setToggleOn} label="Ejemplo" />
              <Toggle value={false} onValueChange={() => {}} label="Apagado" />
              <Toggle value={false} disabled label="Deshabilitado" />
            </View>
          </View>
          <View style={styles.controlGroup}>
            <AppText variant="caption" color="inkMuted">
              SEGMENTED
            </AppText>
            <Segmented
              label="Ejemplo"
              value={segment}
              onChange={setSegment}
              options={[
                { value: 'a', label: 'Uno' },
                { value: 'b', label: 'Dos' },
                { value: 'c', label: 'Tres' },
              ]}
            />
          </View>
          <View style={styles.controlGroup}>
            <TextField label="Nombre del dispositivo" placeholder="Ej. Lámpara del salón" value={text} onChangeText={setText} />
            <TextField label="Con error" placeholder="Campo obligatorio" value="" error="Este campo es obligatorio" />
          </View>
        </Surface>
      </Section>

      <Section title="Chat">
        <Surface>
          <ChatBubble message="Buenas noches. ¿Apago las luces del salón?" time="23:10" />
          <ChatBubble message="Sí, apaga todo por favor." direction="out" time="23:11" />
          <ChatBubble message="Listo. He dejado la lámpara de mesilla al 20%." time="23:11" />
          <ChatInput value={chatText} onChangeText={setChatText} onSend={() => setChatText('')} style={styles.chatInput} />
        </Surface>
      </Section>

      <Section title="Avatares">
        <Surface>
          <View style={styles.inlineRow}>
            <Avatar name="Ana Torres" size={48} />
            <Avatar name="Luis García" size={48} />
            <Avatar name="Marta Ruiz" size={48} />
            <AvatarGroup users={[{ name: 'Ana Torres' }, { name: 'Luis García' }, { name: 'Marta Ruiz' }, { name: 'Eva Luna' }]} size={36} />
          </View>
        </Surface>
      </Section>

      <Section title="Mapa">
        <Surface padding={Spacing.two}>
          <MapPreview
            height={170}
            caption="Casa · Calle Mayor 12"
            markers={[
              { id: 'home', x: 0.5, y: 0.42, tone: 'accent', label: 'Casa' },
              { id: 'cam', x: 0.78, y: 0.68, tone: 'danger' },
            ]}
          />
        </Surface>
      </Section>

      <Section title="Tarjetas de dominio">
        <DeviceCard name="Lámpara de pie" room="Salón" on level={62} detail="62% · 12 W" onToggle={() => {}} />
        <DeviceCard name="Luz de techo" room="Cocina" on={false} level={0} onToggle={() => {}} />
        <View style={[styles.wrapRow, { gap }]}>
          <QuickAction label="Llegar a casa" icon={Icons.home} active style={{ width: halfWidth }} />
          <QuickAction label="Modo noche" icon={Icons.moon} tone="neutral" style={{ width: halfWidth }} />
        </View>
        <View style={styles.wrapRow}>
          <CameraCard name="Entrada" style={{ width: halfWidth }} />
          <CameraCard name="Jardín" live={false} status="Sin señal" time="Hace 20 min" style={{ width: halfWidth }} />
        </View>
      </Section>

      <Section title="Tab bar">
        <Surface padding={Spacing.two}>
          <View style={styles.tabBarDemo}>
            {Object.entries(TAB_CONFIG).map(([name, config], index) => (
              <View key={name} style={styles.tabDemoItem}>
                <View style={[styles.tabDemoPill, index === 0 && styles.tabDemoPillActive]}>
                  <Icon
                    source={config.icon}
                    size={18}
                    color={index === 0 ? Palette.accentInk : Palette.inkMuted}
                  />
                </View>
                <AppText variant="caption" style={{ color: index === 0 ? Palette.accentInk : Palette.inkMuted }}>
                  {config.label}
                </AppText>
              </View>
            ))}
          </View>
        </Surface>
      </Section>

      <Section title="Rejilla responsive">
        <AppText variant="footnote" color="inkMuted">
          Las tarjetas se disponen en {columns} {columns === 1 ? 'columna' : 'columnas'} a {Math.round(width)} px de ancho.
        </AppText>
        <View style={[styles.wrapRow, { gap }]}>
          {['A', 'B', 'C', 'D'].map((label) => (
            <Surface key={label} style={{ width: colWidth }} padding={Spacing.three}>
              <AppText variant="headline">{label}</AppText>
              <AppText variant="footnote" color="inkMuted">
                ancho {Math.round(colWidth)} px
              </AppText>
            </Surface>
          ))}
        </View>
      </Section>
    </Screen>
  );
}

const styles = StyleSheet.create({
  wrapRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: Spacing.two,
  },
  inlineRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.two,
    flexWrap: 'wrap',
  },
  swatch: {
    alignItems: 'center',
    gap: Spacing.half,
    width: 64,
  },
  swatchColor: {
    width: 40,
    height: 40,
    borderRadius: Radii.md,
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: 'rgba(255,255,255,0.8)',
  },
  controlGroup: {
    gap: Spacing.one,
    paddingVertical: Spacing.one,
  },
  chatInput: {
    marginTop: Spacing.two,
  },
  tabBarDemo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    backgroundColor: 'rgba(255,255,255,0.55)',
    borderRadius: Radii.xl,
    padding: Spacing.one,
  },
  tabDemoItem: {
    flex: 1,
    alignItems: 'center',
    gap: 1,
    paddingVertical: Spacing.half,
  },
  tabDemoPill: {
    width: 44,
    height: 30,
    borderRadius: Radii.pill,
    alignItems: 'center',
    justifyContent: 'center',
  },
  tabDemoPillActive: {
    backgroundColor: 'rgba(255,255,255,0.9)',
  },
});
