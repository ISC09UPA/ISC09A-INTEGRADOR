import { useState } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { HeaderApp } from '@/components/HeaderApp';
import { BannerMotor } from '@/components/reglas/BannerMotor';
import { FiltrosReglas } from '@/components/reglas/FiltrosReglas';
import { RuleCard } from '@/components/reglas/RuleCard';
import { REGLAS, type Categoria } from '@/components/reglas/datos';
import { colores } from '@/theme/tokens';

export default function Reglas() {
  const [reglas, setReglas] = useState(REGLAS);
  const [filtro, setFiltro] = useState<Categoria | 'todas'>('todas');

  const visibles = reglas.filter((r) => filtro === 'todas' || r.categoria === filtro);
  const activas = reglas.filter((r) => r.activa).length;

  function alternar(id: string, activa: boolean) {
    setReglas((rs) => rs.map((r) => (r.id === id ? { ...r, activa } : r)));
  }

  return (
    <View style={s.pantalla}>
      <HeaderApp pantalla="Reglas" />
      <ScrollView contentContainerStyle={s.contenido} showsVerticalScrollIndicator={false}>
        <BannerMotor activas={activas} total={reglas.length} />
        <FiltrosReglas seleccionado={filtro} onSelect={setFiltro} />

        {visibles.length === 0 ? (
          <Text style={s.vacio}>Sin reglas en esta categoría.</Text>
        ) : (
          visibles.map((r) => <RuleCard key={r.id} regla={r} onToggle={(a) => alternar(r.id, a)} />)
        )}
      </ScrollView>
    </View>
  );
}

const s = StyleSheet.create({
  pantalla: { flex: 1, backgroundColor: colores.surface },
  contenido: { padding: 16, paddingBottom: 32, gap: 12 },
  vacio: { textAlign: 'center', paddingVertical: 24, color: colores.onSurfaceVariant },
});
