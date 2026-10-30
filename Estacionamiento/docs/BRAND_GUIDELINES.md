# 🛫 AeroPark · Manual de Identidad de Marca y Recursos Visuales

> **Filosofía de Diseño:** *Less is more.*  
> Inspirado en los principios editoriales y minimalistas de **Apple Inc.** (`apple.com` y Apple HIG). La marca se concibe como una silueta pura, nítida y geométrica, sin fondos pesados ni degradados ruidosos, optimizada para flotar como un **overlay transparente** sobre interfaces en modo oscuro, cristal esmerilado y fotografía cinemática de alta definición.

---

## 1. Naming y Arquitectura de Marca

* **Nombre Oficial:** **AeroPark**
* **Nombre Corporativo / Institucional:** **AeroPark Airport Mobility Systems**
* **Tagline:** *Seamless Airport Parking · Tap, Park, Fly*
* **Misión:** Reducir la fricción del estacionamiento en aeropuertos internacionales a cero mediante automatización 100% cashless, reconocimiento biométrico y de placas ANPR, y acceso instantáneo con Apple Wallet / NFC.

---

## 2. El Emblema Central: *The Delta Apex*

El isotipo oficial es **The Delta Apex** (*El Ápice Delta*):
* **Composición Geométrica:** Un perfil alar supersónico simétrico dividido por una ranura central de precisión vertical (el eje de la pista de aterrizaje / guía de carril).
* **Significado:**
  1. **Aeronáutica:** Evoca una aeronave ascendiendo con elegancia y velocidad hacia el cielo.
  2. **Movilidad & Precisión:** Funciona como un puntero de navegación y baliza indicadora hacia el cajón asignado.
  3. **Identidad Standalone:** Es un símbolo icónico independiente, desligado de la tipografía para no confundirse con una letra común.
* **Tratamiento Cromático:** **Blanco puro (`#FFFFFF`)** sobre **fondo 100% transparente**. Sin tarjetas, sin orbes, sin sombras pesadas.

```
       ▲  (Ápice de Navegación)
      / \
     / | \   <-- Ranura de Pista (20px de aire)
    /  |  \
   /___|___\
       ▼
```

---

## 3. Variantes de Logotipo y Archivos Disponibles

Todos los recursos se encuentran versionados en formato vectorial SVG y PNG transparente RGBA supersampleado (4x Lanczos):

| Variante | Archivo Vectorial SVG | Archivo PNG Transparente | Proporción | Uso Principal |
| :--- | :--- | :--- | :--- | :--- |
| **Delta Apex (Mark Standalone)** | [`aeropark_logo_mark.svg`](file:///docs/assets/aeropark_logo_mark.svg) | [`aeropark_logo_mark.png`](file:///docs/assets/aeropark_logo_mark.png) (1024x1024, 512x512, 128x128) | 1:1 | Icono de app, favicons, cabeceras de navegación móvil, marcas de agua de tickets y postes LED |
| **Horizontal Lockup (Emblema + Nombre)** | [`aeropark_logo_horizontal.svg`](file:///docs/assets/aeropark_logo_horizontal.svg) | [`aeropark_logo_horizontal.png`](file:///docs/assets/aeropark_logo_horizontal.png) (1600x400) | 4:1 | Barra superior web, portal administrativo desktop (A-01 a A-40), facturas fiscales y encabezados de reportes |
| **Stacked Lockup (Emblema Centrado + Nombre)** | [`aeropark_logo_stacked.svg`](file:///docs/assets/aeropark_logo_stacked.svg) | [`aeropark_logo_stacked.png`](file:///docs/assets/aeropark_logo_stacked.png) (800x800) | 1:1 | Pantalla de Splash (C-01), bienvenida (C-02), tótems físicos de entrada y terminales de autoservicio |
| **Wordmark (Solo Tipografía Tracked)** | [`aeropark_wordmark.svg`](file:///docs/assets/aeropark_wordmark.svg) | — | 5:1 | Detalles editoriales, pie de página, menciones legales y firmas |

---

## 4. Tipografía y Espaciado (Apple SF Pro Spec)

La jerarquía tipográfica sigue estrictamente la familia del sistema de Apple:

| Rol | Tipografía | Peso | Tracking (Letter Spacing) | Color |
| :--- | :--- | :--- | :--- | :--- |
| **Brand Wordmark** | SF Pro Display / System Sans | SemiBold (600) | `+0.22em` (22px) | `#FFFFFF` |
| **Sub-labels & Taglines** | SF Pro Text / System Sans | Regular (400) | `+0.45em` (11px) | `rgba(255, 255, 255, 0.60)` |
| **Titulares H1/H2** | SF Pro Display | Bold (700) | `-0.02em` | `#FFFFFF` |
| **Cuerpo de Texto** | SF Pro Text | Regular (400) / Medium (500) | `0em` | `rgba(255, 255, 255, 0.85)` |
| **Placas / Timers / Hashes** | SF Mono / JetBrains Mono | Medium (500) | `+0.08em` | `#00F0FF` / `#30D158` |

---

## 5. Paleta Cromática

```
[ Pure White ]       #FFFFFF       (Logos, titulares de alto contraste)
[ Subtle White ]     #A1A1A6       (Textos secundarios, bordes sutiles)
[ Dark Graphite ]    #141517       (Superficie de tarjetas y modales esmerilados)
[ Obsidian Glass ]   #0A0A0C       (Fondo de aplicación móvil y web)
[ Pure Void ]        #000000       (OLED pitch black, barras de estado)
[ Electric Cyan ]    #00F0FF       (Acciones NFC, estados activos, radar)
[ Apple Emerald ]    #30D158       (Pluma levantada, cajón disponible, pago exitoso)
```

---

## 6. Matriz de Asignación de Recursos Visuales por Pantalla

### 📱 Flujos Móviles de Usuario (C-01 a C-34)

| Pantalla / Módulo | Recurso Asignado | Tipo de Elemento | Comportamiento |
| :--- | :--- | :--- | :--- |
| **C-01: Splash Screen** | `aeropark_logo_stacked.svg` | SVG Vector Overlay | Centrado sobre fondo `#000000` con micro-animación de opacidad y escala (0.95 -> 1.0) |
| **C-02: Onboarding & Bienvenida** | `hero_lane.jpg` + `aeropark_logo_horizontal.svg` | Fotografía Cinemática + Logo Overlay | Fotografía de carril inteligente a pantalla completa con viñeta oscura degradada y logotipo blanco flotante |
| **C-03 a C-06: Auth / Biometría** | `aeropark_logo_mark.svg` | SVG Overlay | Marcador superior minimalista (32x32) con indicador de Face ID / Touch ID |
| **C-07 a C-10: Vehículos & Placas** | `aeropark_logo_mark.svg` | SVG Overlay | Badge de marca en cabecera de registro OCR y vinculación de tag NFC |
| **C-11 a C-15: Reserva & Disponibilidad** | `garage_zone.jpg` | Fotografía de Contexto | Fondo ambiental con desenfoque de lente (*background-blur 20px*) para la selección de niveles y zonas |
| **C-16: Dynamic Gate Pass & QR** | `digital_pass.jpg` | Render UI Passbook | Modelo visual de referencia para el pase activo de Apple Wallet con NFC y QR dinámico |
| **C-17 a C-20: Sesión Activa & Wayfinding** | `aeropark_logo_mark.svg` | SVG Overlay | Ícono de cabecera flotante en el HUD de sesión y en la brújula de guiado interior |
| **C-21 a C-24: Tap-to-Pay & Salida** | `hero_lane.jpg` + `aeropark_logo_mark.svg` | Fotografía + Icono | Vista contextual de pluma abierta con confirmación de cobro cashless automático |
| **C-25 a C-28: Wallet & Subscripciones** | `aeropark_logo_horizontal.svg` | SVG Overlay | Tarjeta virtual AeroPark Card con logotipo horizontal blanco en esquina superior derecha |
| **C-33 & C-34: Web Quick-Pay & Kioscos** | `aeropark_logo_stacked.svg` | SVG Overlay | Logotipo centrado para pantallas táctiles de autoservicio y portal web de pago exprés |

---

### 💻 Centro de Operaciones & Desktop Control Room (A-01 a A-40)

| Pantalla / Módulo | Recurso Asignado | Tipo de Elemento | Comportamiento |
| :--- | :--- | :--- | :--- |
| **A-01 a A-04: Digital Twin & Telemetría** | `ops_control.jpg` | Render Arquitectónico | Representación visual del centro de control con mapa 3D isométrico y analítica en tiempo real |
| **Top Navigation Bar (Todas)** | `aeropark_logo_horizontal.svg` | SVG Overlay | Altura 36px, alineado a la izquierda sobre barra de navegación translúcida (*glassmorphism*) |
| **A-05 a A-08: Tarifas & Facturación SAT** | `aeropark_logo_horizontal.svg` | SVG Vector | Membrete oficial en comprobantes CFDI 4.0, reportes contables y liquidaciones bancarias |
| **A-09 a A-12: Seguridad & Multi-Tenant** | `aeropark_logo_mark.svg` | SVG Overlay | Baliza de seguridad en consolas de operadores de pista y tótems de control de accesos |

---

## 7. Componente React Native Disponible

El componente reutilizable [`AeroLogo`](file:///frontend/customer-mobile/src/components/brand/AeroLogo.tsx) se encuentra listo para su uso directo:

```tsx
import { AeroLogo, AeroLogoHorizontal, AeroLogoStacked } from '@/components/brand/AeroLogo';

// 1. Emblema Standalone para cabeceras (Overlay puro sin fondo)
<AeroLogo size={36} color="#FFFFFF" />

// 2. Logotipo Horizontal para barras de navegación
<AeroLogoHorizontal size={40} color="#FFFFFF" showTagline={true} />

// 3. Logotipo Centrado para Splash y Bienvenida
<AeroLogoStacked size={180} color="#FFFFFF" showTagline={true} />
```
