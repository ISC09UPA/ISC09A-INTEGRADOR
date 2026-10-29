import { StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';

import { Icon, Icons } from './icon';
import { AppText } from './text';

import { Radii, Spacing, ToneColors, type Tone } from '@/theme';

export type MapMarker = {
  id: string;
  /** Position as 0–1 fractions of the map area. */
  x: number;
  y: number;
  tone?: Tone;
  label?: string;
};

export type MapPreviewProps = {
  markers?: MapMarker[];
  height?: number;
  caption?: string;
  style?: StyleProp<ViewStyle>;
};

/**
 * Stylized, decorative map surface (fixture art — no real map data).
 * Used as a placeholder for the home location / device zones view.
 */
export function MapPreview({ markers = [], height = 180, caption, style }: MapPreviewProps) {
  return (
    <View style={[styles.map, { height }, style]} accessibilityRole="image" accessibilityLabel={caption ?? 'Mapa'}>
      {/* park blocks */}
      <View style={[styles.block, styles.park, { left: '8%', top: '12%', width: '30%', height: '24%' }]} />
      <View style={[styles.block, styles.park, { right: '10%', bottom: '14%', width: '26%', height: '20%' }]} />
      {/* water strip */}
      <View style={[styles.water, { left: '-5%', top: '58%', width: '110%', height: 18 }]} />
      {/* roads */}
      <View style={[styles.road, { left: 0, top: '46%', width: '100%', height: 10 }]} />
      <View style={[styles.road, { left: '38%', top: 0, width: 10, height: '100%' }]} />
      <View style={[styles.road, { left: '12%', top: 0, width: 6, height: '60%', transform: [{ rotate: '18deg' }] }]} />
      {/* buildings */}
      <View style={[styles.block, styles.building, { left: '48%', top: '14%', width: '18%', height: '16%' }]} />
      <View style={[styles.block, styles.building, { left: '16%', top: '66%', width: '14%', height: '14%' }]} />

      {markers.map((marker) => {
        const tones = ToneColors[marker.tone ?? 'accent'];
        return (
          <View
            key={marker.id}
            style={[
              styles.marker,
              { left: `${marker.x * 100}%`, top: `${marker.y * 100}%`, backgroundColor: tones.bg },
            ]}>
            <Icon source={Icons.pin} size={14} color={tones.fg} />
            {marker.label ? (
              <View style={styles.markerLabel}>
                <AppText variant="caption" color="ink">
                  {marker.label}
                </AppText>
              </View>
            ) : null}
          </View>
        );
      })}

      {caption ? (
        <View style={styles.caption}>
          <AppText variant="caption" color="inkSoft">
            {caption}
          </AppText>
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  map: {
    borderRadius: Radii.lg,
    overflow: 'hidden',
    backgroundColor: '#DCE9E2',
    borderWidth: StyleSheet.hairlineWidth,
    borderColor: 'rgba(255,255,255,0.8)',
  },
  block: {
    position: 'absolute',
    borderRadius: Radii.sm,
  },
  park: {
    backgroundColor: '#C4DEC8',
  },
  building: {
    backgroundColor: '#C9D6DE',
  },
  water: {
    position: 'absolute',
    backgroundColor: '#BFD8EA',
    borderRadius: Radii.pill,
  },
  road: {
    position: 'absolute',
    backgroundColor: 'rgba(255,255,255,0.85)',
    borderRadius: Radii.pill,
  },
  marker: {
    position: 'absolute',
    minWidth: 28,
    height: 28,
    paddingHorizontal: Spacing.one,
    borderRadius: Radii.pill,
    alignItems: 'center',
    justifyContent: 'center',
    flexDirection: 'row',
    gap: 2,
    borderWidth: 2,
    borderColor: 'rgba(255,255,255,0.9)',
  },
  markerLabel: {
    paddingRight: Spacing.half,
  },
  caption: {
    position: 'absolute',
    left: Spacing.two,
    bottom: Spacing.two,
    backgroundColor: 'rgba(255,255,255,0.85)',
    paddingVertical: Spacing.half,
    paddingHorizontal: Spacing.two,
    borderRadius: Radii.pill,
  },
});
