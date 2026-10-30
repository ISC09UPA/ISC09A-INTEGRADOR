import React from 'react';
import Svg, { Polygon, G, Text } from 'react-native-svg';

export interface AeroLogoProps {
  size?: number;
  width?: number;
  height?: number;
  color?: string;
  opacity?: number;
}

export interface AeroLogoLockupProps extends AeroLogoProps {
  showTagline?: boolean;
}

/**
 * AeroLogo - Standalone Delta Apex Mark
 * Pure white vector geometry, 100% transparent background.
 * Unmistakable aeronautical ascent + vertical precision runway slit.
 */
export const AeroLogo: React.FC<AeroLogoProps> = ({
  size = 32,
  color = '#000000',
  opacity = 1.0,
}) => {
  return (
    <Svg width={size} height={size} viewBox="0 0 512 512" fill="none" opacity={opacity}>
      {/* Left Wing Airfoil */}
      <Polygon points="246,92 82,368 194,318 246,389" fill={color} />
      {/* Right Wing Airfoil */}
      <Polygon points="266,92 430,368 318,318 266,389" fill={color} />
    </Svg>
  );
};

/**
 * AeroLogoHorizontal - Delta Apex Mark + "AEROPARK" Wordmark
 * Ideal for navigation headers, web topbars, and desktop operations suites.
 */
export const AeroLogoHorizontal: React.FC<AeroLogoLockupProps> = ({
  size = 40,
  color = '#000000',
  opacity = 1.0,
  showTagline = true,
}) => {
  // Height is size, width is calculated by aspect ratio (4:1)
  const width = size * 4;
  return (
    <Svg width={width} height={size} viewBox="0 0 1600 400" fill="none" opacity={opacity}>
      {/* Delta Apex Emblem */}
      <G transform="translate(60, 50) scale(0.586)">
        <Polygon points="246,92 82,368 194,318 246,389" fill={color} />
        <Polygon points="266,92 430,368 318,318 266,389" fill={color} />
      </G>

      {/* Brand Wordmark */}
      <Text
        x="400"
        y="215"
        fill={color}
        fontFamily="-apple-system, BlinkMacSystemFont, 'SF Pro Display', 'Inter', sans-serif"
        fontSize="94"
        fontWeight="600"
        letterSpacing="22"
      >
        AEROPARK
      </Text>

      {/* Tagline */}
      {showTagline && (
        <Text
          x="406"
          y="270"
          fill={color}
          opacity="0.6"
          fontFamily="-apple-system, BlinkMacSystemFont, 'SF Pro Text', 'Inter', sans-serif"
          fontSize="24"
          fontWeight="400"
          letterSpacing="11"
        >
          AIRPORT MOBILITY · CASHLESS
        </Text>
      )}
    </Svg>
  );
};

/**
 * AeroLogoStacked - Centered Delta Apex Mark + Centered "AEROPARK"
 * Tightly bounded viewBox eliminates unwanted invisible margins while keeping
 * typography large, crisp, and readable.
 */
export const AeroLogoStacked: React.FC<AeroLogoLockupProps> = ({
  size = 180,
  width,
  height,
  color = '#000000',
  opacity = 1.0,
  showTagline = true,
}) => {
  const vbWidth = 800;
  const vbY = 190;
  const vbHeight = showTagline ? 444 : 345;
  const finalWidth = width ?? size;
  const finalHeight = height ?? Math.round(finalWidth * (vbHeight / vbWidth));

  return (
    <Svg
      width={finalWidth}
      height={finalHeight}
      viewBox={`0 ${vbY} ${vbWidth} ${vbHeight}`}
      fill="none"
      opacity={opacity}
    >
      {/* Delta Apex Emblem Centered */}
      <G transform="translate(240, 140) scale(0.625)">
        <Polygon points="246,92 82,368 194,318 246,389" fill={color} />
        <Polygon points="266,92 430,368 318,318 266,389" fill={color} />
      </G>

      {/* Brand Typography Centered */}
      <Text
        x="400"
        y="520"
        textAnchor="middle"
        fill={color}
        fontFamily="-apple-system, BlinkMacSystemFont, 'SF Pro Display', 'Inter', sans-serif"
        fontSize="68"
        fontWeight="700"
        letterSpacing="18"
      >
        AEROPARK
      </Text>

      {showTagline && (
        <Text
          x="400"
          y="575"
          textAnchor="middle"
          fill={color}
          opacity="0.6"
          fontFamily="-apple-system, BlinkMacSystemFont, 'SF Pro Text', 'Inter', sans-serif"
          fontSize="24"
          fontWeight="500"
          letterSpacing="11"
        >
          AIRPORT MOBILITY
        </Text>
      )}
    </Svg>
  );
};

export default AeroLogo;
