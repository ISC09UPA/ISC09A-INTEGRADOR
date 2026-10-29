import { useState } from 'react';
import { StyleSheet } from 'react-native';

import {
  AppHeader,
  Button,
  Icons,
  InfoRow,
  Screen,
  Section,
  Segmented,
  StatusBanner,
  Surface,
} from '@/components';
import { Spacing } from '@/theme';

type Mode = 'desarmado' | 'armado' | 'noche';

const MODE_LABEL: Record<Mode, string> = {
  desarmado: 'Desarmado',
  armado: 'Armado',
  noche: 'Modo noche',
};

export default function SeguridadScreen() {
  const [mode, setMode] = useState<Mode>('armado');

  const armed = mode !== 'desarmado';

  return (
    <Screen background="transparent" tabBarInset>
      <AppHeader eyebrow="Protección" title="Seguridad" subtitle="Sensores, accesos y alarma" />

      <StatusBanner
        tone={armed ? 'success' : 'warning'}
        icon={armed ? Icons.lock : Icons.unlock}
        title={armed ? 'Casa protegida' : 'Alarma desarmada'}
        description={armed ? '8 sensores activos · Última revisión hace 1 min' : 'Los sensores de movimiento están pausados'}
        action={
          <Button
            label={armed ? 'Desarmar' : 'Armar'}
            size="sm"
            variant={armed ? 'danger' : 'primary'}
            onPress={() => setMode(armed ? 'desarmado' : 'armado')}
          />
        }
      />

      <Segmented
        label="Modo de seguridad"
        value={mode}
        onChange={setMode}
        options={[
          { value: 'desarmado', label: 'Desarmado' },
          { value: 'armado', label: 'Armado' },
          { value: 'noche', label: 'Noche' },
        ]}
      />

      <Section title="Accesos">
        <Surface>
          <InfoRow icon={Icons.door} tone="success" label="Puerta principal" description="Cerrada con cerradura inteligente" value="08:12" />
          <InfoRow icon={Icons.lock} tone="success" label="Garaje" description="Puerta cerrada" value="07:45" />
          <InfoRow icon={Icons.door} tone="warning" label="Puerta trasera" description="Abierta" value="12:03" />
        </Surface>
      </Section>

      <Section title="Sensores">
        <Surface>
          <InfoRow icon={Icons.motion} tone={armed ? 'success' : 'neutral'} label="Movimiento · Salón" value={armed ? 'Activo' : 'Pausado'} />
          <InfoRow icon={Icons.motion} tone={armed ? 'success' : 'neutral'} label="Movimiento · Pasillo" value={armed ? 'Activo' : 'Pausado'} />
          <InfoRow icon={Icons.bell} tone="success" label="Detector de humo" value="Normal" />
          <InfoRow icon={Icons.drop} tone="success" label="Fuga de agua · Cocina" value="Normal" />
        </Surface>
      </Section>

      <Section title="Modo actual">
        <Surface style={styles.modeCard} padding={Spacing.two}>
          <InfoRow
            label={MODE_LABEL[mode]}
            description="Cambiado hoy a las 08:12"
            icon={Icons.shield}
            tone={armed ? 'success' : 'warning'}
            value={armed ? 'Protegido' : 'Sin protección'}
          />
        </Surface>
      </Section>
    </Screen>
  );
}

const styles = StyleSheet.create({
  modeCard: {},
});
