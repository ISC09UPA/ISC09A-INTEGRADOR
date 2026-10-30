

export const ApplePalette = {
  // Light Canvas (Apple / Executive Automotive)
  light: {
    canvas: "#FFFFFF",
    surfacePrimary: "#F8FAFC",
    surfaceSecondary: "#F1F5F9",
    surfaceTranslucent: "rgba(255, 255, 255, 0.92)",
    borderHairline: "rgba(15, 23, 42, 0.08)",
    textPrimary: "#0F172A",
    textSecondary: "#475569",
    textTertiary: "#94A3B8",
    accent: "#0066CC",
    accentHover: "#0055B3",
    success: "#16A34A",
    warning: "#D97706",
    destructive: "#DC2626",
  },
  // Dark Canvas (Executive Architectural Dark)
  dark: {
    canvas: "#0F172A",
    surfacePrimary: "#1E293B",
    surfaceSecondary: "#334155",
    surfaceTranslucent: "rgba(15, 23, 42, 0.85)",
    borderHairline: "rgba(255, 255, 255, 0.12)",
    textPrimary: "#F8FAFC",
    textSecondary: "#94A3B8",
    textTertiary: "#64748B",
    accent: "#38BDF8",
    accentHover: "#0284C7",
    success: "#22C55E",
    warning: "#F59E0B",
    destructive: "#EF4444",
  },
};

export const Typography = {
  fontFamilies: {
    display: "SF Pro Display",
    text: "SF Pro Text",
    mono: "SF Mono",
  },
  editorialScale: {
    hero: { fontSize: 48, lineHeight: 54, letterSpacing: -1.2, fontWeight: "700" as const },
    largeTitle: { fontSize: 34, lineHeight: 41, letterSpacing: -0.7, fontWeight: "700" as const },
    title1: { fontSize: 28, lineHeight: 34, letterSpacing: -0.4, fontWeight: "600" as const },
    headline: { fontSize: 17, lineHeight: 22, letterSpacing: -0.4, fontWeight: "600" as const },
    body: { fontSize: 17, lineHeight: 24, letterSpacing: -0.4, fontWeight: "400" as const },
    callout: { fontSize: 15, lineHeight: 20, letterSpacing: -0.2, fontWeight: "400" as const },
    footnote: { fontSize: 13, lineHeight: 18, letterSpacing: -0.1, fontWeight: "400" as const },
    eyebrow: { fontSize: 11, lineHeight: 14, letterSpacing: 0.9, fontWeight: "700" as const, textTransform: "uppercase" as const },
  },
};

export const Radii = {
  pill: 980,          // Botones en píldora estilo Apple (apple.com CTAs)
  squircleLarge: 28,  // Tarjetas Bento Grid
  squircleMedium: 20, // Modales y Bottom Sheets
  squircleSmall: 12,  // Inputs y chips
};

export const Motion = {
  appleEase: "cubic-bezier(0.16, 1, 0.3, 1)",
  springConfig: {
    damping: 18,
    stiffness: 120,
    mass: 0.9,
  },
};
