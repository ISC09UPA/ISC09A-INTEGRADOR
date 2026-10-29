import { router } from 'expo-router';
import { StyleSheet, View } from 'react-native';

import {
  AppHeader,
  Avatar,
  Button,
  Icons,
  InfoRow,
  MapPreview,
  Metric,
  QuickAction,
  Screen,
  Section,
  StatusBanner,
  Surface,
  DeviceCard,
} from '@/components';
import { Spacing } from '@/theme';

export default function InicioScreen() {
  return (
    <Screen background="transparent" tabBarInset>
      <AppHeader
        showBrand
        title="Buenos días"
        subtitle="Miércoles · Tu hogar está tranquilo"
        right={<Avatar name="Ana Torres" size={44} />}
      />

      <StatusBanner
        tone="success"
        icon={Icons.check}
        title="Todo en orden"
        description="4 dispositivos activos · Sincronizado hace 2 min"
      />

      <View style={styles.metricsRow}>
        <Metric
          style={styles.metric}
          label="Salón"
          value="22.5"
          unit="°C"
          tone="accent"
          icon={Icons.thermometer}
          caption="Confortable"
        />
        <Metric
          style={styles.metric}
          label="Consumo"
          value="1.2"
          unit="kW"
          tone="warning"
          icon={Icons.bolt}
          caption="−8% vs ayer"
        />
      </View>

      <Section title="Accesos rápidos">
        <View style={styles.actionsRow}>
          <QuickAction label="Llegar a casa" icon={Icons.home} active />
          <QuickAction label="Modo noche" icon={Icons.moon} tone="neutral" />
          <QuickAction label="Alarma" icon={Icons.shield} tone="success" />
          <QuickAction label="Todo apagado" icon={Icons.power} tone="danger" />
        </View>
      </Section>

      <Section
        title="Dispositivos"
        action={<Button label="Ver todos" variant="ghost" size="sm" onPress={() => router.push('/luces')} />}>
        <DeviceCard
          name="Lámpara del salón"
          room="Salón"
          on
          level={62}
          detail="62% · 12 W"
          onPress={() => router.push('/luces')}
        />
        <DeviceCard
          name="Tira LED"
          room="Dormitorio"
          on={false}
          level={0}
          onPress={() => router.push('/luces')}
        />
      </Section>

      <Section title="Tu hogar">
        <Surface padding={Spacing.two}>
          <MapPreview
            height={170}
            caption="Casa · Calle Mayor 12"
            markers={[
              { id: 'home', x: 0.5, y: 0.42, tone: 'accent', label: 'Casa' },
              { id: 'cam', x: 0.78, y: 0.68, tone: 'danger' },
            ]}
          />
          <View style={styles.mapInfo}>
            <InfoRow icon={Icons.wifi} tone="success" label="Router" value="En línea" />
            <InfoRow icon={Icons.person} tone="accent" label="En casa" value="2 personas" />
          </View>
        </Surface>
      </Section>

      <Section title="Diseño">
        <Surface padding={Spacing.two}>
          <InfoRow
            icon={Icons.grid}
            tone="accent"
            label="Catálogo de componentes"
            description="Variantes de UI del diseño"
            showChevron
            onPress={() => router.push('/catalog')}
          />
        </Surface>
      </Section>
    </Screen>
  );
}

const styles = StyleSheet.create({
  metricsRow: {
    flexDirection: 'row',
    gap: Spacing.two,
  },
  metric: {
    flex: 1,
  },
  actionsRow: {
    flexDirection: 'row',
    gap: Spacing.two,
  },
  mapInfo: {
    marginTop: Spacing.one,
    gap: Spacing.half,
  },
});
