# Plan de desarrollo: Estacionamiento inteligente de aeropuerto

**Fecha:** Oct 16, 2026  
**Autor:** @Alejandro  
**Enfoque:** Operación 100% Digital / Cashless (Pagos exclusivamente en línea) & Diseño Minimalista de Alta Gama Inspirado en Sitios Web de Apple (apple.com) & Apple HIG

---

## 1. Visión, objetivos y alcance

El producto permite que un vehículo entre y salga del estacionamiento del aeropuerto sin ticket físico ni intercambio de efectivo: la cámara lee su placa, el sistema decide si puede pasar, y el cliente reserva y paga en línea desde el celular mediante la aplicación (o Apple Pay / portal web ágil), mientras el personal supervisa toda la operación desde un panel de escritorio de estética refinada.

### Problema
Las filas en barrera, los tickets perdidos, la manipulación de efectivo en casetas, el cobro manual propenso a mermas y la falta de visibilidad de cupo generan demoras, fugas de ingreso, costos de transporte de valores y quejas. Además, la seguridad no tiene forma de saber en tiempo real si un vehículo con reporte ingresó al recinto.

### Propuesta de valor
- **Cliente:** Experiencia sin fricción tipo Apple (*seamless*): reservar cajón, entrar y salir sin detenerse por tickets, pagar con un toque vía **Apple Pay**, tarjeta o app, y localizar su auto con una interfaz minimalista, limpia y fluida.
- **Operación:** Ecosistema 100% digital y sin efectivo (*cashless*), ocupación en tiempo real, conciliación automática con pasarela de pagos (PSP) y eliminación de cajas físicas en caseta.
- **Seguridad:** Alerta inmediata, revisada minuciosamente por personal calificado, ante coincidencias con bases de reporte vehicular.

### Métricas de éxito del MVP (a medir desde el piloto)

| Métrica | Meta | Cómo se mide |
| :--- | :--- | :--- |
| Tiempo de detección a apertura de pluma | $\le$ 3 s (p95) | Telemetría del edge y del Access Service |
| Exactitud de lectura de placa | $\ge$ 97 % de día, $\ge$ 90 % de noche o lluvia | Muestra auditada de 500 eventos por semana |
| Disponibilidad del flujo de acceso | 99.9 % mensual | Azure Monitor, health checks |
| Transacciones 100% digitales en línea | 100 % (Cero efectivo) | Bitácora de transacciones y conciliación PSP |
| Reservas sobre total de entradas | $\ge$ 30 % a los 3 meses | Panel de KPIs |
| Alertas policiales revisadas por una persona | 100 % | Auditoría del Police Gateway |
| Tiempo de resolución de una incidencia en el panel | < 5 min (mediana) | Registro de incidencias |

### Alcance

#### Dentro del alcance del MVP
- Una entrada y una salida con cámara inteligente, sensor de presencia, pluma automatizada y pantalla LED informativa.
- Lectura de placa (LPR/ANPR), reglas de acceso (reserva, rotación, lista blanca, lista negra) y bitácora con evidencia fotográfica.
- **Pagos exclusivamente en línea:** Cobro automático al salir, Apple Pay, Google Pay, tarjetas tokenizadas en la app y portal web de pago express para usuarios ocasionales.
- App cliente (iOS, Android y Web responsive): Registro biométrico, gestión de vehículos, reservas, pago 1-tap, historial, QR de respaldo y notificaciones push.
- Panel admin (Desktop Web): Tablero en vivo, monitoreo de accesos, incidencias operativas, gestión de tarifas, conciliación bancaria digital y auditoría.
- Consulta a reportes policiales con revisión humana obligatoria (simulada hasta firma de convenio).

#### Fuera del alcance del MVP
- Cobro en efectivo o cajeros automáticos físicos (el sistema es nativamente 100% digital en línea).
- Sensores por cajón y guiado individual a lugar libre (fase 2).
- Multi-terminal y multi-aeropuerto simultáneo (el modelo de datos lo contempla, la operación inicial no).
- Facturación CFDI automática y convenios con aerolíneas (fase 3).
- Cobro e integración con servicios de terceros (valet parking, agencias de renta de autos).

---

## 2. Personas y roles

El sistema atiende al usuario final en su dispositivo móvil y a cuatro roles internos en consola de escritorio; cada rol interactúa con interfaces depuradas y toda acción crítica queda auditada.

### Arquetipos de clientes
- **Laura (Viajera frecuente):** Llega con prisa, equipaje de mano y desea entrar y salir sin interactuar con casetas ni sacar tarjetas físicas. Valora ver con total claridad el desglose de tarifas, pagar con Apple Pay o débito automático y recibir su recibo digital de inmediato. Prefiere interfaces fluidas, limpias y sin pantallas recargadas.
- **Marcos (Viaje ocasional):** No desea descargar apps ni crear cuentas extensas. Al salir, escanea el código QR dinámico de la pantalla del carril con su iPhone/Android y completa el pago en línea en 10 segundos desde la web express con Apple Pay o tarjeta bancaria.

### Matriz de roles del panel administrativo

| Rol (panel admin) | Qué hace | Puede | No puede |
| :--- | :--- | :--- | :--- |
| **Operador** | Monitorea el carril y atiende incidencias en tiempo real | Ver tablero en vivo, apertura manual de pluma con motivo justificado, guiar al usuario en pago en línea, resolver incidencias | Cambiar tarifas, ver reportes policiales, administrar usuarios *(No maneja dinero físico)* |
| **Supervisor** | Asegura la continuidad operativa y la salud financiera digital | Todo lo del operador, conciliación de liquidaciones PSP, emisión de reembolsos digitales hasta un tope, reportes de turno | Administrar usuarios del sistema, ver datos policiales |
| **Seguridad** | Recibe y dictamina alertas de placas con reporte | Ver alertas policiales y evidencia fotográfica lado a lado, confirmar, descartar o escalar | Cobros, tarifas, configuración operativa |
| **Administrador** | Configuración integral de la plataforma | Gestión de esquemas tarifarios, zonas, dispositivos IoT, usuarios, roles, auditoría completa y retención | Alterar bitácoras de auditoría (solo lectura estricta) |

**Reglas de acceso:** MFA obligatorio para todos los roles corporativos, sesiones de 8 horas con reautenticación para acciones sensibles (reembolsos digitales, apertura forzada, cambio de tarifas) y prohibición de cuentas compartidas.

---

## 3. Decisiones técnicas, principios de desarrollo y arquitectura escalable

El stack arquitectónico está concebido para máxima estabilidad, alta velocidad, escalabilidad elástica y seguridad de nivel bancario: backend en **.NET 10 (LTS)** con Minimal APIs, arquitectura limpia (*Clean Architecture*) y principios SOLID; aplicación cliente en **React Native** con integración móvil profunda (iOS y Android), aprovechando capacidades nativas del hardware (cámara para escaneo OCR, biometría Face ID/Touch ID/BiometricPrompt, Apple Pay, Google Pay y almacenamiento seguro en Keychain/Keystore); base de datos transaccional en **Azure Database for MySQL (Flexible Server)** con réplicas de lectura y connection pooling optimizado; agentes edge en **Python 3.11+** sobre Raspberry Pi; y servicios de infraestructura en **Microsoft Azure**.

### Registro de decisiones de arquitectura (ADRs)

| ID | Decisión | Alternativa descartada | Motivo |
| :--- | :--- | :--- | :--- |
| **ADR-01** | Monolito modular en Azure Container Apps, con Police Gateway aislado | Microservicios distribuidos desde el día 1 | Menor costo y sobrecarga operativa en el MVP sin sacrificar fronteras de dominio ni modularidad |
| **ADR-02** | Minimal APIs con endpoint groups, CQRS (MediatR) y servicios inyectados | Controladores MVC tradicionales | Rendimiento superior, menor consumo de memoria, código conciso y desacoplamiento estricto |
| **ADR-03** | Frontend en **React Native** con integración móvil profunda (iOS/Android) y panel admin desacoplado | Aplicación híbrida genérica sin soporte nativo de hardware | Experiencia de usuario nativa tipo Apple HIG, acceso directo a hardware biométrico, cámara, Apple Pay / Google Pay y SecureStore |
| **ADR-04** | Cliente TypeScript generado desde OpenAPI | Modelos y tipos manuales | Eliminación de desfases entre contratos de API y pantallas de la aplicación |
| **ADR-05** | IoT Hub con DPS y certificados X.509 por dispositivo | Endpoints HTTP públicos | Seguridad criptográfica por hardware, revocación granular y canal bidireccional seguro |
| **ADR-06** | OCR híbrido: inferencia local de recorte + Azure AI Vision (con OCR local de contingencia) | 100% en la nube o 100% local | Máxima precisión con baja latencia y operación continua sin internet |
| **ADR-07** | **Azure Database for MySQL (Flexible Server)** (transaccional ACID), Cosmos DB (eventos LPR), Redis (estado vivo), Blob Storage (imágenes) | Base de datos monolítica no relacional o SQL Server tradicional | MySQL en Azure ofrece alta escalabilidad con réplicas de lectura, rendimiento probado en transacciones concurrentes, soporte de cifrado nativo TDE y compatibilidad abierta |
| **ADR-08** | Toda coincidencia policial genera alerta a operador humano; pluma nunca se bloquea sola | Bloqueo o retención automática | Protección de usuarios inocentes ante falsos positivos de lectura de placa |
| **ADR-09** | Entra External ID (clientes) y Entra ID con MFA (personal interno) con hashing local de contingencia | Autenticación en texto plano o cifrado simétrico reversible | Máximo cumplimiento normativo, biometría nativa y mitigación de fuga de credenciales |
| **ADR-10** | **Operación 100% Cashless (En línea exclusivo):** PSP con soporte Apple Pay, Google Pay y tokenización de tarjetas | Receptores de billetes/monedas en caseta o cajeros físicos | Cero riesgo de asalto/merma, sin costo de traslado de valores, flujo de salida 5 veces más ágil |
| **ADR-11** | **Diseño minimalista inspirado en sitios web de Apple (apple.com) & Apple HIG:** Tipografía editorial SF Pro, retículas Bento Grid, botones en píldora, barras de navegación translúcidas y microinteracciones fluidas | Diseños genéricos, recargados o sin jerarquía editorial | Experiencia visual icónica, limpia y memorable que transmite máxima confianza, sofisticación y facilidad de uso |
| **ADR-12** | **Desarrollo con buenas prácticas para alta escalabilidad:** Clean Architecture, SOLID, CQRS, APIs stateless y réplicas de lectura en MySQL | Código espagueti o arquitectura en capas acopladas | Permite que el sistema escale horizontalmente bajo picos de demanda masiva sin degradación de latencia |
| **ADR-13** | **Cifrado integral de información sensible en tránsito y en reposo:** TLS 1.3, TDE en Azure MySQL, AES-256-GCM a nivel de columna, Azure Key Vault y Keychain/Keystore móvil | Cifrado únicamente perimetral | Protección contra fugas de datos conforme a LFPDPPP y estándares de seguridad bancaria |
| **ADR-14** | **Irreversibilidad absoluta de contraseñas:** Hashing unidireccional con **Argon2id** + Salt criptográfico de 128 bits único por usuario | Cifrado reversible o hashes obsoletos (MD5/SHA1/SHA256 sin sal) | Imposibilidad matemática y criptográfica de descifrar contraseñas, incluso ante exfiltración total de la base de datos |

### 3.1 Buenas prácticas de ingeniería y arquitectura para alta escalabilidad

Para garantizar que el software soporte desde un carril piloto hasta múltiples terminales aeroportuarias con miles de accesos simultáneos sin degradación de rendimiento, el proyecto se rige por las siguientes directrices de escalabilidad:

#### A. Arquitectura limpia (*Clean Architecture*) y principios SOLID en .NET
- **Separación de capas en 4 anillos concéntricos:**
  1. **Core / Domain:** Entidades puras de negocio (`Booking`, `Stay`, `Vehicle`, `Tariff`), objetos de valor (*Value Objects* como `PlateNumber`, `Money`), interfaces de repositorios y eventos de dominio. Libre de dependencias de bases de datos o librerías de terceros.
  2. **Application:** Casos de uso orquestados mediante comandos y consultas (*CQRS*) con **MediatR**, validaciones declarativas con **FluentValidation**, DTOs y mapeos.
  3. **Infrastructure:** Implementación de persistencia con **Entity Framework Core 10** y el proveedor **Pomelo.EntityFrameworkCore.MySql**, clientes HTTP resilientes con Polly, adaptadores para Azure Key Vault, Azure Blob Storage y pasarelas de pago.
  4. **API / Presentation:** Minimal APIs agrupadas por dominio (*Endpoint Filters* para validación, autenticación y manejo global de excepciones en formato RFC 7807 *Problem Details*).
- **Principios SOLID rigurosos:** Inversión de dependencias mediante contenedor IoC nativo de ASP.NET Core; interfaces segregadas y específicas (`IAccessDecisionService`, `IPricingService`, `IPaymentService`); clases y métodos con responsabilidad única (*Single Responsibility*).

#### B. Escalabilidad de la base de datos: Azure Database for MySQL (Flexible Server)
- **Desacoplamiento de lecturas y escrituras (*Read Replicas*):**
  - El nodo primario atiende exclusivamente transacciones de escritura de alta consistencia (ingresos, cobros, reservas confirmadas).
  - Las réplicas de lectura en Azure absorben las consultas analíticas del panel administrativo, búsquedas de auditoría y monitores de reportes, evitando contención de bloqueos (*table/row locks*).
- **Gestión eficiente de conexiones (*Connection Pooling*):**
  - Implementación con `MySqlConnector` y Pomelo con límites de tamaño de pool configurados según la concurrencia esperada.
  - Reutilización de conexiones para eliminar la sobrecarga de aperturas TCP/TLS repetitivas.
- **Indexación estratégica y optimización de consultas:**
  - Índices compuestos en columnas de alta selectividad: `(placaNormalizada, estado)`, `(customerId, estado)`, `(fechaInicio, fechaFin)`.
  - Uso estricto de consultas sin seguimiento (`AsNoTracking()`) en todas las lecturas para reducir la presión sobre el recolector de basura (GC).
  - Particionado lógico o por rango temporal en tablas de alto crecimiento (`AccessEvent`, `AuditEntry`).

#### C. Escalabilidad horizontal del Backend (.NET)
- **Servicios completamente sin estado (*Stateless APIs*):** Ninguna instancia retiene estado de sesión en memoria local. Todo estado efímero se gestiona en **Azure Cache for Redis**.
- **Auto-escalado elástico:** Despliegue en **Azure Container Apps** con reglas KEDA basadas en volumen de solicitudes HTTP concurrentes y uso de CPU/memoria.
- **Resiliencia y procesamiento asíncrono:** Patrón *Retry con Exponential Backoff* y *Circuit Breaker* (Polly) para llamadas a servicios externos (PSP, Azure AI Vision). Tareas pesadas (conciliaciones, generación de reportes masivos) procesadas en segundo plano mediante Azure Service Bus / Background Workers.

#### D. Buenas prácticas y escalabilidad en React Native (Integración Móvil)
- **Arquitectura modular basada en funcionalidades (*Feature-Based Structure*):** Carpetas encapsuladas por dominio (`features/bookings`, `features/vehicles`, `features/payments`, `features/access`) que incluyen componentes, hooks, servicios y tipos propios.
- **Integración profunda con hardware móvil:**
  - **Cámara & ML:** Captura optimizada y escaneo óptico de placa mediante módulos nativos de alto rendimiento (`react-native-vision-camera` / MLKit) sin bloquear el hilo principal de JavaScript.
  - **Biometría nativa:** Autenticación fluida con Face ID / Touch ID (iOS) y BiometricPrompt (Android) mediante `expo-local-authentication`.
  - **Pasarelas nativas:** Integración nativa de SDKs de Apple Pay (PassKit) y Google Pay para procesamiento en 1 toque.
  - **Almacenamiento seguro por hardware:** Llaves de sesión y tokens JWT almacenados en el **Secure Enclave** (iOS Keychain) y **TEE** (Android Keystore) mediante `expo-secure-store`.
- **Optimización de renderizado y fluidez (60/120 FPS):**
  - Uso riguroso de `React.memo`, `useMemo` y `useCallback` en componentes de listas y cronómetros de estancia activa.
  - Listas de alto rendimiento con `FlashList` (Shopify) en lugar del `FlatList` tradicional para scroll instantáneo en historial y vehículos.
  - Manejo de estado predecible y liviano con **Zustand** o Redux Toolkit, evitando renderizados masivos del árbol de componentes.

### 3.2 Estándar de cifrado integral de información sensible

Toda información clasificada como confidencial, sensible o de identificación personal (PII) está sujeta a políticas de cifrado estricto tanto en reposo como en tránsito:

1. **Cifrado en tránsito (Data in Transit):**
   - **TLS 1.3 obligatorio** en todas las conexiones externas e internas (App React Native $\leftrightarrow$ Backend .NET $\leftrightarrow$ Azure Database for MySQL).
   - Encabezados de seguridad estrictos: HTTP Strict Transport Security (HSTS con `max-age=31536000; includeSubDomains; preload`).
   - Autenticación mutua (**mTLS**) para nodos edge Raspberry Pi y el microservicio Police Gateway.
2. **Cifrado en reposo (Data at Rest):**
   - **Azure Database for MySQL (Flexible Server):** Cifrado de almacenamiento nativo con Transparent Data Encryption (TDE) utilizando claves gestionadas por el cliente (*Customer Managed Keys - CMK*) alojadas y rotadas en **Azure Key Vault**.
   - **Cifrado a nivel de columna / aplicación (Application-Level Encryption):** Los atributos de datos altamente sensibles (números telefónicos de usuarios, correos electrónicos en contextos reservados, detalles de dictámenes de seguridad policial y payloads de auditoría confidenciales) se cifran mediante **AES-256-GCM** (con vector de inicialización único de 96 bits y tag de autenticación de 128 bits) en la capa de infraestructura de .NET antes de insertarse en MySQL.
   - **Almacenamiento de archivos y evidencias:** Contenedores en **Azure Blob Storage** cifrados con Server-Side Encryption (SSE) AES-256; acceso exclusivo mediante URLs pre-firmadas con Shared Access Signatures (**SAS Tokens**) de vigencia efímera (máximo 15 minutos).
   - **Dispositivo móvil (React Native):** Tokens de sesión, identificadores y datos en caché local cifrados en reposo utilizando `SecureStore`, que delega la custodia de claves criptográficas al hardware de seguridad del teléfono (iOS Keychain con Secure Enclave / Android Keystore respaldado por hardware).

### 3.3 Seguridad criptográfica de contraseñas: Imposibilidad de descifrado

Las contraseñas de usuarios finales y del personal administrativo se gestionan bajo el principio de **irreversibilidad criptográfica absoluta**:

1. **Imposibilidad de descifrado:**
   - **Bajo ninguna circunstancia se almacenan contraseñas en texto claro ni mediante algoritmos de cifrado simétrico o asimétrico reversible.**
   - No existe ninguna clave de desencriptación ni función inversa que permita reconstruir la contraseña original a partir del registro almacenado. Ni desarrolladores, ni administradores de sistemas, ni actores maliciosos que accedan a la base de datos pueden descifrar las contraseñas.
2. **Algoritmo de Hashing unidireccional de última generación:**
   - Se implementa **Argon2id** (ganador oficial del Password Hashing Competition y recomendado por OWASP) para la derivación y almacenamiento de contraseñas.
   - Parámetros mínimos de configuración:
     - Memoria (*Memory Cost*): $65,536\text{ KiB}$ ($64\text{ MB}$).
     - Iteraciones (*Time Cost*): $\ge 3$ iteraciones.
     - Paralelismo (*Degree of Parallelism*): $4$ hilos.
   - *Alternativa de contingencia homologada:* PBKDF2-HMAC-SHA256 con un factor de trabajo de $\ge 600,000$ iteraciones (estándar OWASP 2024+).
3. **Salt criptográfico único y aleatorio por usuario:**
   - Cada contraseña se mezcla con una sal aleatoria única de **128 bits (16 bytes)** generada mediante un generador criptográficamente seguro de números aleatorios (`RandomNumberGenerator.GetBytes(16)`).
   - El salt impide por completo el uso de tablas precalculadas (*Rainbow Tables*) o ataques de diccionario cruzado entre usuarios.
4. **Validación segura y mitigación de canales laterales:**
   - La comprobación de contraseñas durante el inicio de sesión se efectúa recalculando el hash del texto introducido con el salt registrado y comparándolo mediante funciones de **tiempo constante** (`CryptographicOperations.FixedTimeEquals`), anulando vulnerabilidades por análisis de tiempos de respuesta (*Timing Attacks*).
5. **Políticas defensivas complementarias:**
   - Limitación estricta de tasa (*Rate Limiting*) en endpoints de autenticación (`POST /auth/login` máximo 5 intentos por IP/cuenta por minuto).
   - Bloqueo progresivo temporal tras 5 intentos fallidos consecutivos.
   - Verificación de contraseñas comprometidas contra listas globales de filtraciones (k-anonymity vía API HaveIBeenPwned).

### Supuestos abiertos (confirmar antes del Sprint 1)
- **Tipo de proyecto:** Producto con estándares de producción comercial en Azure.
- **Capacidad piloto:** 1 carril de entrada, 1 de salida y aproximadamente 500 cajones rotativos.
- **Base policial:** Simulada mediante API Mock con contratos reales hasta formalizar el convenio de colaboración.
- **Pasarela de pago (PSP):** Stripe o Conekta con integración directa de **Apple Pay**, Google Pay y tokenización PCI-DSS SAQ A.

---

## 4. Filosofía de diseño: Minimalismo extremo y estética inspirada en los sitios web de Apple (apple.com) & Apple HIG

Para lograr una experiencia de usuario memorable, moderna, limpia y de máxima confianza, tanto la aplicación móvil del cliente (**React Native**), el portal web express de salida (**C-34**) como el panel administrativo adoptan el lenguaje visual y la elegancia minimalista de los sitios web de **Apple (apple.com)** y las directrices **Apple HIG**:

### 4.1 Principios rectores del diseño minimalista estilo Apple Websites (apple.com)
1. **Tipografía monumental con ritmo editorial:**
   - Inspirada en la tipografía de gran formato de apple.com: titulares audaces en **SF Pro Display** con interletrado negativo ajustado (`letter-spacing: -0.025em`), contrastados con subencabezados limpios en gris neutro editorial (`#86868B`).
   - Uso de *Eyebrows* (etiquetas superiores en mayúsculas condensadas o títulos pequeños) en el azul icónico de Apple (`#0071E3`) o gris cálido, ubicados inmediatamente arriba de los títulos para categorizar la vista (ej. `RESERVA INTELIGENTE`, `ACCESO EXPRESS`, `TARIFAS EN VIVO`).
2. **Espacio negativo expansivo (*Whitespace* generoso):**
   - El vacío visual es un elemento activo de diseño. Márgenes y paddings amplios eliminan cualquier saturación gráfica, obligando a la mirada a enfocarse con naturalidad en el dato fundamental: matrícula, costo acumulado en tiempo real, código QR dinámico o botón de pago.
3. **Retículas Bento Grid Asimétricas (Apple Product Layout):**
   - Estructuración modular de paneles y dashboards mediante retículas estilo **Bento Grid** (las mismas empleadas en las páginas de producto del iPhone, Mac y Apple Watch).
   - Módulos con radios continuos tipo *Squircle* generosos ($24\text{ px}$ a $32\text{ px}$), combinando tarjetas cuadradas de datos compactos con tarjetas panorámicas de estancia activa o métricas de flujo vehicular.
4. **Navegación superior translúcida (*Apple Global Nav*):**
   - Barra superior adhesiva (*Sticky Navigation Bar*) con altura compacta de $44\text{ px}$ a $48\text{ px}$ y acabado de vidrio esmerilado translúcido (*Frosted Glass* con `backdrop-filter: blur(20px)` y `rgba(255, 255, 255, 0.8)` en tema claro o `rgba(22, 22, 23, 0.8)` en tema oscuro).
   - Línea inferior de delimitación ultra-sutil de $0.5\text{ px}$ con opacidad del $6\%$, evitando sombras pesadas o bordes marcados.
5. **Botones en Píldora (*Pill CTAs*) y Enlaces con Chevron:**
   - Botones primarios con forma de píldora continua (`border-radius: 980px`), ya sea en azul Apple (`#0071E3`) o negro azabache (`#000000`) con tipografía en blanco, que reaccionan con una micro-escala suave ($1.02\times$) al interactuar.
   - Enlaces secundarios con el característico chevron animado de Apple: *"Conocer más ›"*, *"Ver desglose de tarifa ›"*, *"Descargar comprobante ›"*, con sutil desplazamiento lateral en hover.
6. **Profundidad óptica y bordes milimétricos:**
   - Ausencia total de gradientes estridentes, bordes gruesos o sombras densas. Las capas de elevación se logran mediante cambios sutiles de tonalidad en superficies cerámicas y sombras de amplia dispersión con opacidad casi invisible (`box-shadow: 0 8px 30px rgba(0, 0, 0, 0.04)`).

### 4.2 Tokens de diseño y paleta cromática exacta (Apple Web Palette)
- **Light Canvas (Estética apple.com diurna):**
  - *Fondo base de página:* Blanco inmaculado (`#FFFFFF`).
  - *Fondo de tarjetas y Bento Grid:* Gris cerámico suave (`#F5F5F7`) y gris titanio claro (`#FBFBFD`).
  - *Texto primario:* Negro carbón de lectura suave (`#1D1D1F`), evitando el negro puro para reducir la fatiga visual.
  - *Texto secundario y metadatos:* Gris neutro calibrado (`#86868B`).
  - *Líneas y bordes milimétricos:* Hairline de $0.5\text{ px}$ en `rgba(0, 0, 0, 0.06)`.
- **Dark Canvas (Estética Apple Pro / Modo Oscuro profundo):**
  - *Fondo base de página:* Negro puro OLED (`#000000`).
  - *Fondo de tarjetas y Bento Grid:* Gris espacial profundo (`#161617`) y grafito refinado (`#1D1D1F`).
  - *Texto primario:* Blanco puro de alto contraste (`#F5F5F7`).
  - *Texto secundario:* Gris plata suave (`#86868B`).
  - *Líneas y bordes milimétricos:* Hairline de $0.5\text{ px}$ en `rgba(255, 255, 255, 0.12)`.
- **Acentos de color icónicos de Apple:**
  - *Azul Apple:* `#0071E3` en Light / `#2997FF` en Dark (utilizado en CTAs primarios, enlaces activos y badges de estado).
  - *Verde Éxito:* `#34C759` en Light / `#30D158` en Dark (confirmación de cobro, pluma abierta, reserva confirmada).
  - *Rojo Alerta:* `#FF3B30` en Light / `#FF453A` en Dark (fallos de tarjeta, reporte de seguridad).
  - *Ámbar Atención:* `#FF9500` (tolerancia de tiempo excedida, mantenimiento).
- **Tipografía y jerarquía:**
  - Familia primaria: **SF Pro Display** (títulos, contadores de estancia, números de importe) y **SF Pro Text** (párrafos, formularios, controles).
  - Familia monospace: **SF Mono** para placas vehiculares (estilo matrícula metálica estampada), folios bancarios y tiempos ISO.
  - Escala editorial:
    - *Hero Display:* 48 pt / 56 pt semibold (`letter-spacing: -0.025em`).
    - *Large Title:* 34 pt bold (`letter-spacing: -0.02em`).
    - *Title 1:* 28 pt bold (`letter-spacing: -0.015em`).
    - *Headline:* 17 pt semibold (`letter-spacing: -0.01em`).
    - *Body Text:* 17 pt regular (altura de línea 1.47, máxima legibilidad).
    - *Callout / Footnote:* 13 pt a 15 pt regular en `#86868B`.
    - *Eyebrow Badge:* 11 pt bold en mayúsculas con espaciado amplio (`letter-spacing: 0.08em`).

### 4.3 Físicas de animación, microinteracciones y transiciones de scroll
- **Curva de animación Apple Web:** Curva cúbica `cubic-bezier(0.16, 1, 0.3, 1)` para todas las transiciones de estado, hovers de botones y apertura de modales, logrando un movimiento orgánico que desacelera con elegancia natural.
- **Scroll Storytelling y revelación progresiva:** Al deslizar la página, las tarjetas y secciones Bento emergen con un desvanecimiento vertical sutil (*fade-and-rise* de $16\text{ px}$ de traslación).
- **Micro-interacciones táctiles (React Native Móvil):** Físicas de resorte (*spring animations* con amortiguación crítica $\zeta = 0.85$) en botones de 1-Tap, selector de placas y descarte de hojas inferiores (*Bottom Sheets*).
- **Haptic Feedback refinado:** Respuesta háptica auditiva y táctil en dispositivos iOS y Android (`UIImpactFeedbackGenerator(.light)` en selecciones y `.medium` al autorizar con Apple Pay).

---

## 6. Épicas e historias de usuario

Hay 12 épicas; las 9 primeras constituyen el MVP y las restantes completan fases subsiguientes. Prioridad MoSCoW: **M** (Must have), **S** (Should have), **C** (Could have).

### Resumen de épicas

| Épica | Nombre | Fase | Prioridad |
| :--- | :--- | :--- | :--- |
| **E1** | Fundaciones: repo, CI/CD, IaC, identidad | 0 | M |
| **E2** | Edge: captura y lectura de placa | 1 | M |
| **E3** | Motor de decisión de acceso | 1 | M |
| **E4** | Cuenta y vehículos del cliente | 1 | M |
| **E5** | Reservas | 1 | M |
| **E6** | Pagos 100% en línea y cobro automático | 2 | M |
| **E7** | Panel de operación en vivo | 2 | M |
| **E8** | Incidencias y seguridad (alertas policiales) | 2 | M |
| **E9** | Administración y configuración | 2 | M |
| **E10** | Notificaciones | 2 | S |
| **E11** | Ocupación por cajón y guiado | 3 | S |
| **E12** | Hardening, carga y actualización de SDK | 3 | M |

### Historias de usuario y criterios de aceptación

| ID | Historia | Criterios de aceptación | Pri. |
| :--- | :--- | :--- | :--- |
| **E1-01** | Como equipo, quiero un monorepo con CI para que cada cambio se valide solo | Pipeline compila, ejecuta pruebas y linters en cada PR; rama main protegida; empaquetado de ambas apps Expo y de la API .NET | M |
| **E1-02** | Como equipo, quiero infraestructura como código para recrear entornos | Bicep o Terraform despliega dev, test y prod de forma reproducible en menos de 30 min | M |
| **E1-03** | Como usuario, quiero autenticarme de forma segura y moderna | Login con Entra External ID / Entra ID corporativo, soporte biométrico (FaceID/TouchID), tokens seguros y MFA interno | M |
| **E2-01** | Como Pi, quiero detectar presencia vehicular para disparar la captura de video | Sensor inductivo activa ráfaga en < 300 ms; filtro anti-rebote para evitar lecturas duplicadas | M |
| **E2-02** | Como edge, quiero recortar la placa del mejor fotograma | Detección YOLO ligera en la Pi en < 500 ms; preservación del fotograma íntegro para evidencia | M |
| **E2-03** | Como sistema, quiero normalizar y evaluar la placa | Texto estandarizado en mayúsculas, cálculo de confianza; si está por debajo del umbral, se deriva a incidencia | M |
| **E2-04** | Como edge, quiero continuidad operativa sin conexión a internet | Cola SQLite FIFO local; reenvío ordenado e idempotente al reanudar conectividad; modo degradado autónomo | M |
| **E3-01** | Como sistema, quiero validar en tiempo real el ingreso | Autoriza si existe reserva válida, rotación con cupo o lista blanca; tiempo de respuesta < 1 s | M |
| **E3-02** | Como sistema, quiero auditar cada paso con evidencia fotográfica | Bitácora inmutable con placa, estampa de tiempo, carril, decisión, motivo y URL segura de la imagen | M |
| **E3-03** | Como operador, quiero apertura forzada de pluma en contingencias | Requiere motivo justificado, reautenticación y queda registrado en la bitácora inalterable | M |
| **E3-04** | Como sistema, quiero computar la tarifa exacta de salida | Cálculo automático por tiempo, tope diario, tolerancia de salida y totalización digital libre de redondeos ambiguos | M |
| **E4-01** | Como cliente, quiero registrarme rápidamente con mi teléfono o correo | Registro completado en < 2 min; interfaz minimalista tipo Apple; confirmación con código OTP de 6 dígitos | M |
| **E4-02** | Como cliente, quiero asociar mis vehículos | Registro de hasta 5 placas; componente visual tipo placa metálica; validación de unicidad entre cuentas activas | M |
| **E4-03** | Como cliente, quiero ejercer mi derecho de borrado de cuenta | Proceso de anonimización o baja en 30 días, siempre que no existan cobros pendientes ni estancias en curso | M |
| **E5-01** | Como cliente, quiero reservar cajón con desglose transparente | Selector de fechas estilo calendario iOS; precio final 100% visible antes del pago; vinculación a placa | M |
| **E5-02** | Como cliente, quiero modificar o cancelar reservas | Visualización de políticas de reembolso; actualización de saldo y confirmación inmediata | M |
| **E5-03** | Como cliente, quiero un pase digital QR de contingencia | QR firmado de un solo uso en Apple Wallet / app que permite el acceso si la placa está sucia o ilegible | S |
| **E6-01** | Como cliente, quiero pagar en línea con Apple Pay, Google Pay o tarjeta guardada | Integración nativa de SDK con checkout de 1 toque; 3DS 2.0 dinámico; la plataforma nunca toca datos de tarjeta | M |
| **E6-02** | Como cliente, quiero débito automático al cruzar la salida | Al leer la placa saliente, se liquida en línea contra el método preferido; pluma abre de inmediato y llega recibo digital | M |
| **E6-03** | Como supervisor, quiero conciliar el cierre del turno sin lidiar con efectivo | Reporte automatizado de transacciones procesadas por el PSP vs eventos de salida; marcas de discrepancia | M |
| **E7-01** | Como operador, quiero monitorear el flujo vehicular en tiempo real | Tablero minimalista con actualización por SignalR en < 2 s; ocupación, cobros digitales y estado del hardware | M |
| **E7-02** | Como operador, quiero buscar el historial de cualquier placa | Búsqueda global instantánea con atajo `/`; despliegue de línea de tiempo y evidencia fotográfica | M |
| **E8-01** | Como oficial de seguridad, quiero alertas discretas de reportes policiales | Notificación visual elegante con foto, confianza y datos del reporte; sin interferir automáticamente con la barrera | M |
| **E8-02** | Como oficial de seguridad, quiero dictaminar alertas de forma documentada | Opciones de Confirmar, Descartar (falso positivo) o Escalar; comentario obligatorio y firma digital | M |
| **E8-03** | Como administrador, quiero supervisar la auditoría de seguridad | Bitácora de solo lectura de todas las consultas realizadas al gateway policial | M |
| **E9-01** | Como administrador, quiero parametrizar tarifas y tolerancias | Versionado de tarifas con fecha futura de entrada en vigor; simulador visual antes de publicar | M |
| **E9-02** | Como administrador, quiero controlar accesos y roles del personal | Asignación con principio de menor privilegio; revocación inmediata y doble confirmación | M |
| **E9-03** | Como administrador, quiero gestionar dispositivos IoT en los carriles | Monitoreo de telemetría (temperatura, memoria, cámara), reinicio remoto y rotación de certificados | M |
| **E10-01** | Como cliente, quiero notificaciones push claras y oportunas | Avisos al ingresar, recordatorio de vencimiento de estancia y recibo de pago digital al instante | S |
| **E11-01** | Como cliente, quiero ubicar en qué zona dejé mi automóvil | Tarjeta visual en la app con nivel, zona y mapa peatonal de retorno | S |
| **E12-01** | Como equipo, quiero certificar la plataforma bajo estrés | Pruebas de carga a 3x del volumen pico sin degradar los tiempos de apertura de pluma ($\le$ 3 s) | M |
| **E12-02** | Como equipo, quiero actualización periódica del stack tecnológico | Suite de pruebas automatizadas y soporte para las últimas guías de Apple y Google Play | M |

---

## 7. App cliente (iOS, Android y web responsive)

La aplicación cliente ofrece una experiencia visual premium, pulida y minimalista, inspirada en las aplicaciones nativas de Apple. Se organiza en **34 pantallas agrupadas en 6 módulos**, con navegación inferior en móvil mediante barra flotante translúcida (*Floating Tab Bar* con efectos de desenfoque) y barra lateral fija en pantallas de escritorio $\ge$ 768 px.

### Directrices visuales específicas de la app
- **Pagos exclusivamente digitales:** En ningún punto se solicita pago en efectivo ni se dirige a casetas físicas. Los métodos disponibles son **Apple Pay**, Google Pay y tarjetas de crédito/débito guardadas con tokenización.
- **Tipografía e iconos:** SF Pro Text/Display en iOS y web; iconos con trazos finos consistentes con SF Symbols.
- **Componentes Apple:**
  - *Bottom Sheets* deslizables para selección de vehículos, opciones de pago y confirmaciones.
  - *Segmented Controls* estilo iOS para alternar vistas (ej. Próximas / Activas / Pasadas).
  - *Live Activity / Dynamic Island (fase 2):* Indicador en tiempo real del tiempo de estancia transcurrido.
  - Componente de Placa Vehicular metálica con relieve sutil y tipografía monospace.

### Catálogo de pantallas de la app cliente

#### Grupo A. Entrada y bienvenida
| ID | Pantalla | Contenido y acciones (Estilo Apple) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **C-01** | Carga inicial (Splash) | Logo minimalista monocromático, transición suave, validación de sesión biométrica | Versión obsoleta conduce a C-33 | `GET /app/config` |
| **C-02** | Bienvenida | Hero visual elegante, 3 tarjetas de beneficios clave, botones primario (*Continuar con Apple / Correo*) y secundario (*Pagar sin cuenta*) | Solo se presenta en primer inicio | N/A |
| **C-03** | Iniciar sesión | Input limpio con autocompletado, autenticación FaceID/TouchID, botón Apple Sign-In | Credenciales inválidas, bloqueo tras 5 intentos, sin red | Entra External ID (OIDC) |
| **C-04** | Registro | Nombre, correo o teléfono, contraseña con medidor minimalista y aceptación de privacidad | Validación reactiva, correo duplicado | `POST /customers` |
| **C-05** | Validación de código | 6 celdas numéricas estilizadas con autoenfoque y respuesta háptica; reenvío en 30 s | Código caducado o inválido | `POST /customers/verify` |
| **C-06** | Consentimientos | Resumen de términos con interruptores nativos (Toggles iOS) y visualización modal del texto legal | Bloqueo si no se acepta consentimiento obligatorio | `PUT /customers/me/consents` |
| **C-07** | Recuperar contraseña | Formulario de un solo campo, envío de magic link o código de recuperación | Mensaje de éxito genérico para evitar filtrado de usuarios | Entra External ID |
| **C-08** | Permisos del sistema | Solicitud transparente de Notificaciones Push y Localización para ubicar el auto | Si el usuario rechaza, la app continúa informando limitaciones | N/A |

#### Grupo B. Inicio y vehículos
| ID | Pantalla | Contenido y acciones (Estilo Apple) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **C-09** | Inicio | Tarjeta destacada translúcida (*Glass card*) con estancia activa o próxima reserva, botón circular de reserva rápida y aforo | Sin reservas: ilustración minimalista; sin red: caché local | `GET /home` |
| **C-10** | Mis vehículos | Lista de tarjetas con placas en relieve, alias, modelo y distintivo de predeterminado | Vacío: invitación a registrar primer vehículo | `GET /vehicles` |
| **C-11** | Alta / Edición de vehículo | Formulario con selector de marca/color y escaneo óptico de placa con cámara (MLKit) | Placa inválida, ya asignada a otra cuenta, máximo 5 | `POST` y `PUT /vehicles` |

#### Grupo C. Reservas en línea
| ID | Pantalla | Contenido y acciones (Estilo Apple) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **C-12** | Búsqueda de disponibilidad | Selector de fechas tipo rueda iOS (*Wheel Picker*) o calendario minimalista; tipo de cajón | Fechas en el pasado, salida menor a entrada | `GET /availability` |
| **C-13** | Cotización y opciones | Tarjetas limpias con desglose de tarifa neta, impuestos y políticas de cancelación | Sin cupo: sugerencia de horarios; precio actualizado | `GET /quotes` |
| **C-14** | Resumen de reserva (Sheet) | Modal deslizante con datos de la estancia, selector de placa y botón directo de pago | Cupo agotado durante la sesión | `POST /bookings` |
| **C-15** | Checkout en línea (1-Tap) | **Botón nativo Apple Pay**, Google Pay o tarjeta guardada; sin cobro en caseta | Tarjeta declinada, 3DS cancelado, tiempo límite de 10 min | `POST /payments/intents` |
| **C-16** | Reserva confirmada | Animación de confirmación sutil, número de folio, QR de respaldo y botón *Agregar a Apple Wallet* | Pago pendiente: estado informativo sin emisión de pase | `GET /bookings/{id}` |
| **C-17** | Mis reservas | Segmented Control (*Próximas*, *Activas*, *Historial*) con transiciones suaves | Estado vacío por cada filtro | `GET /bookings` |
| **C-18** | Detalle de reserva | Información completa, código QR con brillo automático al abrir, acciones Modificar/Cancelar | Reserva vencida o en curso | `GET /bookings/{id}` |
| **C-19** | Modificar reserva | Ajuste de horario o cambio de placa con cálculo de diferencia en tiempo real | Sin cupo para el nuevo intervalo seleccionado | `PATCH /bookings/{id}` |
| **C-20** | Cancelación y reembolso | Modal informativo con cálculo de devolución automática a la tarjeta original | Fuera de la ventana de cancelación gratuita | `POST /bookings/{id}/cancel` |

#### Grupo D. Estancia activa y salida digital
| ID | Pantalla | Contenido y acciones (Estilo Apple) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **C-21** | Estancia activa | Contador de tiempo en tipografía mono-espaciada grande, costo acumulado en vivo y botón extender | Marcador de costo estimado; aviso si excedió la reserva | `GET /stays/active` |
| **C-22** | Dónde está mi auto | Tarjeta con nivel y zona; mapa vectorial minimalista con ruta peatonal paso a paso | Modo básico (solo texto de zona si no hay sensores individuales) | `GET /stays/{id}/location` |
| **C-23** | Pago digital de salida | Desglose final de estancia; **pago en 1 toque vía Apple Pay o tarjeta en línea**; pluma abre sola | Tarjeta rechazada: selector de método alternativo | `POST /payments/exit` |
| **C-24** | Recibo digital | Comprobante desglosado con diseño de ticket limpio, opción de exportar PDF o enviar a Mail | Generación pendiente: reintento asíncrono | `GET /receipts/{id}` |

#### Grupo E. Métodos de pago, perfil y privacidad
| ID | Pantalla | Contenido y acciones (Estilo Apple) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **C-25** | Billetera y pagos | Tarjetas guardadas con logos de franquicia en miniatura, gestión de Apple Pay predeterminado | Sin métodos guardados | `GET /payment-methods` |
| **C-26** | Agregar tarjeta | Componente de campos seguros provisto por el PSP (la app no almacena el PAN) | Datos inválidos, autenticación 3DS requerida | `POST /payment-methods/setup` |
| **C-27** | Historial de transacciones | Listado agrupado por meses con filtros minimalistas por vehículo | Sin movimientos en el periodo | `GET /payments` |
| **C-28** | Ajustes de perfil | Avatar, información personal, accesos a seguridad biométrica y soporte | Sesión expirada | `GET /customers/me` |
| **C-29** | Editar datos | Modificación de nombre, teléfono y correo con verificación secundaria | Teléfono o correo en uso por otra cuenta | `PUT /customers/me` |
| **C-30** | Preferencias de alertas | Toggles nativos para avisos push y correo de estancias y promociones | Alertas transaccionales y de seguridad no desactivables | `PUT /customers/me/notifications` |
| **C-31** | Centro de privacidad (ARCO) | Consulta de consentimientos, descarga de paquete de datos y solicitud de eliminación | Bloqueo de eliminación si existe estancia o saldo pendiente | `POST /customers/me/export`, `DELETE /customers/me` |
| **C-32** | Asistencia y soporte | Centro de ayuda estilo FAQ desplegable (*Accordions* minimalistas) y chat directo | Sin red: despliegue de teléfonos de asistencia rápida | `POST /support/tickets` |

#### Grupo F. Estados globales y pago express para invitados
| ID | Pantalla | Contenido y acciones (Estilo Apple) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **C-33** | Pantallas de sistema | Vistas limpias para *Sin conexión*, *Mantenimiento* y *Actualización requerida* con botón primario | Enlace directo a App Store / Google Play | `GET /app/config` |
| **C-34** | Pago sin cuenta (Web express) | Ingreso de placa, despliegue del monto y **pago inmediato con Apple Pay o tarjeta en línea** | Placa no encontrada o ya liquidada; protección anti-fuerza bruta | `POST /guest/lookup`, `POST /guest/pay` |

---

## 8. Panel admin (solo escritorio - estética macOS)

El panel administrativo traslada la elegancia y sobriedad de las aplicaciones nativas de **macOS** a un entorno web de escritorio optimizado para pantallas $\ge$ 1280 px. Se estructura con barra lateral translúcida (*Vibrancy*), área de contenido con alto ratio de datos por píxel sin saturación, y centro de notificaciones contextual.

### Menú lateral por rol (Operación 100% Cashless)

| Sección | Operador | Supervisor | Seguridad | Administrador |
| :--- | :--- | :--- | :--- | :--- |
| **Tablero y accesos** | Sí | Sí | Solo lectura | Sí |
| **Incidencias de carril** | Sí | Sí | Sí | Sí |
| **Alertas policiales** | No | No | Sí | Solo auditoría |
| **Finanzas (Pagos en línea)** | Consulta de estados | Conciliación PSP | No | Completo |
| **Reservas y clientes** | Consulta | Sí | Consulta | Sí |
| **Configuración** | No | Consulta | No | Sí |
| **Auditoría y reportes** | No | Reportes | Sus alertas | Sí |

### Reglas transversales de UI (macOS Style)
- **Cero gestión de efectivo:** No existen pantallas de arqueo físico de billetes, faltantes de caja en caseta ni custodia de valores. Todo el flujo financiero es 100% digital vía pasarela en línea.
- **Tablas minimalistas de datos:** Fondos alternados con sutileza, sin bordes pesados, ordenamiento por columnas, filtros guardables y exportación a CSV/Excel.
- **Atajos de teclado globales:** Búsqueda rápida con `/`, apertura manual con `Ctrl+Shift+P`, navegación de registros con flechas.
- **Micro-estados y SignalR:** Actualizaciones en tiempo real sin recargar página; badges con pulso de color para eventos recientes.
- **Modo nocturno perfeccionado:** Paleta oscura profunda que reduce la fatiga visual de los operadores en turnos nocturnos.

### Catálogo de pantallas del panel administrativo

#### Grupo A. Acceso y estructura
| ID | Pantalla | Contenido y acciones (Estilo macOS) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **A-01** | Login corporativo | Pantalla limpia, botón Microsoft Entra ID con MFA | Credenciales inválidas, usuario inactivo | Entra ID (OIDC) |
| **A-02** | Pantalla no compatible | Aviso de diseño exclusivo para escritorio en pantallas < 1024 px | Activación automática por viewport | N/A |
| **A-03** | Shell de aplicación | Barra lateral translúcida, barra de herramientas superior con selector de terminal y estado de conexión | Banner de desconexión SignalR | `GET /admin/me` |
| **A-04** | Registro de turno | Apertura y cierre de sesión operativa por carril/terminal | Incidencias pendientes bloquean cierre no supervisado | `POST /admin/shifts` |

#### Grupo B. Operación en vivo
| ID | Pantalla | Contenido y acciones (Estilo macOS) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **A-05** | Dashboard en vivo | Indicadores en tarjetas limpias (aforo actual, flujo vehicular por hora, ingresos en línea, estado de barreras) | Dispositivo desconectado resaltado en rojo suave | `GET /admin/dashboard`, SignalR |
| **A-06** | Monitor de accesos | Feed de lecturas en tiempo real con foto de placa, carril, decisión y porcentaje de confianza | Pausar feed, resaltar lecturas con baja confianza | `GET /admin/access-events` |
| **A-07** | Detalle de acceso | Comparativa de recorte vs fotograma completo, corrección manual de placa y reevaluación inmediata | Reevaluación requiere motivo obligatorio | `GET` y `PATCH /admin/access-events/{id}` |
| **A-08** | Apertura manual de barrera | Modal de seguridad con carril seleccionado, motivo justificado y reautenticación | Sin respuesta de hardware: reintento guiado | `POST /admin/gates/{id}/open` |
| **A-09** | Cola de incidencias | Lista priorizada (placa no leída, pago digital fallido, barrera trabada) con temporizador SLA | Alerta visual ante incidencias fuera de tiempo | `GET /admin/incidents` |
| **A-10** | Resolución de incidencia | Evidencia vinculada, asignación a operador y cierre documentado con causa raíz | Causa raíz obligatoria | `PATCH /admin/incidents/{id}` |
| **A-11** | Búsqueda global | Consulta unificada por placa que muestra reservas, estancias y cobros digitales | Sin coincidencias | `GET /admin/search` |
| **A-12** | Monitor de ocupación | Vista gráfica minimalista de zonas y niveles con porcentajes de saturación | Datos sincronizados con Redis | `GET /admin/occupancy` |

#### Grupo C. Seguridad y control policial
| ID | Pantalla | Contenido y acciones (Estilo macOS) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **A-13** | Bandeja de alertas | Notificaciones de placas con reporte policial; visualización de evidencia y tipo de orden | Fallo de conexión con gateway policial | `GET /security/alerts` |
| **A-14** | Dictamen de alerta | Pantalla dividida: evidencia fotográfica vs reporte oficial; botones *Confirmar*, *Descartar* o *Escalar* | Bloqueo concurrente si otro oficial ya atiende | `POST /security/alerts/{id}/decision` |
| **A-15** | Histórico de alertas | Registro de alertas evaluadas con filtros por resultado y oficial responsable | Exportación protegida por permisos | `GET /security/alerts/history` |

#### Grupo D. Finanzas y conciliación 100% digital
| ID | Pantalla | Contenido y acciones (Estilo macOS) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **A-16** | Transacciones en línea | Registro de todos los pagos digitales procesados vía app, Apple Pay o web express | Filtros por estado (aprobado, pendiente, devuelto) | `GET /admin/payments` |
| **A-17** | Detalle de cobro | Algoritmo de cálculo de tarifa aplicado, identificador de transacción en PSP y marcas de tiempo | Detección de anomalías tarifarias | `GET /admin/payments/{id}` |
| **A-18** | Reembolsos en línea | Modal para procesar devoluciones parciales o totales a la tarjeta original con reautenticación | Montos superiores al tope exigen aval de supervisor | `POST /admin/payments/{id}/refund` |
| **A-19** | Cierre de turno digital | Balance automático de pagos en línea procesados por el PSP vs vehículos salidos del turno | Discrepancias generan aviso para revisión | `POST /admin/shifts/{id}/close` |
| **A-20** | Conciliación con PSP | Cruce de liquidaciones bancarias del PSP con cobros registrados en la base de datos | Diferencias generan reporte automático | `GET /admin/reconciliation` |

#### Grupo E. Gestión de clientes y reservas
| ID | Pantalla | Contenido y acciones (Estilo macOS) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **A-21** | Lista de reservas | Tabla de reservaciones con filtros de fechas, estados y placas | Filtros combinados sin resultado | `GET /admin/bookings` |
| **A-22** | Gestión de reserva | Detalle de reserva, pagos en línea asociados, reenvío de QR de respaldo o cancelación | Acciones no permitidas según ciclo de vida | `GET` y `PATCH /admin/bookings/{id}` |
| **A-23** | Clientes | Padrón de usuarios registrados con información sensible enmascarada | Búsqueda por placa, correo o nombre | `GET /admin/customers` |
| **A-24** | Ficha de cliente | Vehículos, estancias históricas, pagos efectuados y solicitudes de privacidad ARCO | Bloqueo de supresión con adeudo o estancia | `GET /admin/customers/{id}` |
| **A-25** | Listas de control | Mantenimiento de Lista Blanca (personal, VIP) y Lista Restringida con vigencias | Detección de placas en conflicto | `GET` y `POST /admin/lists` |

#### Grupo F. Configuración avanzada
| ID | Pantalla | Contenido y acciones (Estilo macOS) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **A-26** | Esquemas de tarifas | Historial de versiones tarifarias con fechas de vigencia y estado | Validación de una sola tarifa activa | `GET /admin/tariffs` |
| **A-27** | Editor tarifario | Definición de costos por fracción, topes por día, periodos de gracia y simulador visual | Aprobación por segundo administrador requerida | `POST /admin/tariffs` |
| **A-28** | Zonas y cajones | Parametrización de capacidades por nivel y zona | Bloqueo al reducir capacidad bajo reservas ya confirmadas | `GET` y `PUT /admin/zones` |
| **A-29** | Nodos edge (Raspberry Pi) | Monitoreo del estado de cada carril, versión de software, latencia y salud de sensores | Pérdida de latido > 2 min detona incidencia | `GET /admin/devices` |
| **A-30** | Control de hardware | Visualización de cámara en vivo, prueba de sensor de lazo y reinicio remoto | Timeout de comando en 30 s | `POST /admin/devices/{id}/commands` |
| **A-31** | Usuarios y roles | Directorio de empleados internos, roles asignados y estatus de MFA | Mínimo dos administradores activos | `GET /admin/users` |
| **A-32** | Edición de usuario | Modificación de roles y permisos con confirmación de seguridad | Validación de políticas corporativas | `POST` y `PATCH /admin/users` |
| **A-33** | Parámetros del sistema | Configuración de umbral OCR, ventanas de retención de datos y reglas de sesión | Cambios quedan versionados | `GET` y `PUT /admin/settings` |

#### Grupo G. Auditoría, reportes y estado
| ID | Pantalla | Contenido y acciones (Estilo macOS) | Estados y errores | Endpoint |
| :--- | :--- | :--- | :--- | :--- |
| **A-34** | Bitácora general | Registro inmutable de acciones (*quién, qué, cuándo, IP, valores previos/posteriores*) | Solo lectura inmodificable | `GET /admin/audit` |
| **A-35** | Auditoría policial | Log detallado de todas las consultas realizadas al gateway policial | Restringido a auditores y administrador | `GET /admin/audit/police` |
| **A-36** | Centro de reportes | Métricas de aforo, ingresos 100% digitales, precisión de OCR y tiempos de respuesta | Reportes pesados generados en segundo plano | `GET /admin/reports` |
| **A-37** | Descargas seguras | Repositorio de reportes exportados con caducidad automática de 7 días | Descargas auditadas | `GET /admin/exports` |
| **A-38** | Telemetría y salud | Estado en vivo de servicios en Azure, colas de mensajería y latencias de API | Alerta inmediata de servicio degradado | `GET /admin/health` |
| **A-39** | Perfil de operador | Preferencias de tema (claro/oscuro), atajos de teclado y sesiones activas | N/A | `GET` y `PUT /admin/me` |
| **A-40** | Pantallas de excepción | Vistas elegantes para sesión expirada, 403 Forbidden y 404 Not Found con acción de retorno | N/A | N/A |

---

## 9. Edge: Raspberry Pi, cámara y sensores

Cada carril funciona como una estación autónoma equipada con hardware industrial. La Raspberry Pi ejecuta las tareas de captura, recorte y comunicación, garantizando que el flujo físico no se interrumpa.

### Hardware por carril

| Componente | Recomendación | Nota técnica |
| :--- | :--- | :--- |
| **Computadora** | Raspberry Pi 5 (4 GB) en chasis de aluminio industrial, SSD NVMe y UPS HAT | El uso de SSD previene fallos por cortes eléctricos repentinos |
| **Cámara** | Cámara IP PoE (2 a 4 MP), obturador global (*global shutter*), lente varifocal e IR | Enfoque optimizado a placas en movimiento (1 a 1.5 m de altura, ángulo < 30°) |
| **Detección de presencia** | Lazo inductivo empotrado (sensor primario) + radar mmWave (apoyo) | Confiabilidad máxima bajo condiciones de lluvia, niebla y viento |
| **Control de barrera** | Módulo de relé optoacoplado con contacto seco al controlador de la pluma | Lazo de seguridad anti-aplastamiento independiente por hardware |
| **Seguridad física** | Botón de apertura de emergencia manual cableado directo a la pluma | Conexión cableada directa sin pasar por el software |
| **Pantalla informativa** | Display LED o pantalla LCD de alto brillo por RS485/MQTT | Muestra mensajes de bienvenida, aforo o **código QR dinámico para pago en línea** |
| **Conectividad** | Conexión Ethernet principal con módem celular 4G de conmutación automática | Red VLAN aislada con certificados mTLS |

### Flujos de operación del carril

#### Flujo de entrada
1. El vehículo activa el lazo inductivo; la Pi dispara una captura de 3 a 5 fotogramas en menos de 500 ms.
2. Inferencia ligera con modelo YOLO (ONNX Runtime / TFLite) para recortar la matrícula vehicular.
3. El recorte se envía a Azure AI Vision para OCR; si no responde en 800 ms, se recurre al motor OCR local (Tesseract/PaddleOCR).
4. La Pi normaliza la cadena de texto y calcula el índice de confianza.
5. El evento se despacha al Access Service vía HTTPS y a Azure IoT Hub para telemetría.
6. Si la decisión es favorable (*reserva activa, lista blanca o rotación con cupo disponible*), el relé acciona la apertura; el vehículo avanza y el lazo secundario confirma el paso cerrando la pluma.
7. Si la lectura presenta baja confianza, la pantalla solicita reintento o asistencia, generando una incidencia en el panel admin.

#### Flujo de salida (100% Cashless)
1. El vehículo se sitúa en la barrera de salida y la cámara detecta la placa.
2. El sistema calcula la tarifa exacta acumulada.
3. **Escenario A (Usuario con tarjeta o Apple Pay registrado):** El cobro se procesa automáticamente en línea en < 1.5 s; el pago es confirmado y la pluma se abre de inmediato sin necesidad de interacción manual.
4. **Escenario B (Usuario sin cobro automático configurado):** La pantalla del carril presenta un **código QR dinámico**. El conductor lo escanea con su smartphone, abriendo la pasarela express web en la que liquida en segundos vía **Apple Pay**, Google Pay o tarjeta en línea. Al recibirse la confirmación del PSP por webhook, la pluma se abre automáticamente.

### Máquina de estados del carril

| Estado | Evento de entrada | Evento de salida |
| :--- | :--- | :--- |
| **Reposo** | Sistema listo o ciclo anterior concluido | Sensor inductivo detecta presencia |
| **Captura** | Presencia vehicular confirmada | Ráfaga de fotogramas completada o timeout (2 s) |
| **Reconocimiento** | Imagen válida disponible | Matrícula normalizada con score de confianza |
| **Decisión** | Datos de lectura listos | Respuesta del backend o timeout de contingencia (2 s) |
| **Apertura** | Decisión = "Abrir" | Sensor valida paso vehicular o tiempo límite (30 s) |
| **Cobro en línea** | Salida pendiente de liquidación digital | Pago confirmado vía PSP o derivación a asistencia |
| **Revisión manual** | Matrícula ilegible o incidencia | Operador valida y autoriza desde el panel admin |
| **Degradado (Offline)** | Falla prolongada de conexión a internet | Restablecimiento de conectividad con la nube |

### Modo degradado (sin internet)
- **Caché local sincronizada:** La Raspberry Pi actualiza cada 5 minutos una copia en SQLite de la lista blanca y de las reservas confirmadas del día.
- **Acceso offline:** Si la placa coincide con la base local, la barrera se abre. En salida, los usuarios con reserva previa salen sin demora.
- **Almacenamiento FIFO:** Todos los eventos ocurridos en modo offline se almacenan en SQLite local con marca de tiempo original y clave de idempotencia, sincronizándose en orden al regresar la conexión.
- **Seguridad policial en offline:** No se ejecutan consultas externas; las matrículas registradas se encolan para verificación diferida al retornar la red.

---

## 10. Backend: datos, servicios y eventos

Arquitectura en **.NET 10** construida con Minimal APIs, arquitectura limpia (*Clean Architecture*), CQRS (MediatR), Entity Framework Core 10 con proveedor Pomelo MySQL, inyección de dependencias estricta y separación limpia entre contratos, aplicación y persistencia.

### Modelo de persistencia

| Entidad | Atributos clave | Motor de persistencia | Medidas de seguridad y cifrado |
| :--- | :--- | :--- | :--- |
| **Customer** | `id`, `nombre`, `correo`, `teléfono`, `idioma`, `estado`, `externalId`, `passwordHash`, `passwordSalt` | Azure Database for MySQL (Flexible Server) | `teléfono` y `correo` cifrados con AES-256-GCM a nivel de columna; `passwordHash` generado con **Argon2id** + salt de 128 bits (imposible de descifrar) |
| **Consent** | `customerId`, `tipo`, `versiónAviso`, `fechaConsentimiento`, `ip` | Azure Database for MySQL (Flexible Server) | Cifrado en reposo TDE (Azure Key Vault) |
| **Vehicle** | `id`, `customerId`, `placaNormalizada`, `alias`, `marca`, `modelo`, `color` | Azure Database for MySQL (Flexible Server) | Índice único por placa para cuentas activas; TDE |
| **Terminal y Zone** | `id`, `nombre`, `capacidadTotal`, `tipoCajón`, `horario` | Azure Database for MySQL (Flexible Server) | TDE en reposo; replicado en caché Redis |
| **Booking** | `id`, `customerId`, `vehicleId`, `zoneId`, `desde`, `hasta`, `estado`, `montoTotal` | Azure Database for MySQL (Flexible Server) | Transaccional ACID con aislamiento snapshot |
| **Stay** | `id`, `bookingId` (opcional), `placa`, `ingreso`, `salida`, `estadoCobro` | Azure Database for MySQL (Flexible Server) | Índices cubrientes en `(placa, ingreso, salida)` |
| **AccessEvent** | `id`, `deviceId`, `carril`, `placaLeída`, `placaCorregida`, `confianza`, `decisión`, `evidenciaUri`, `hora` | Cosmos DB (particionado por `terminalId`) | TDE con clave CMK en Key Vault |
| **Tariff** | `id`, `versión`, `tramosMinutos`, `costoTramo`, `topeDiario`, `toleranciaMinutos`, `vigencia` | Azure Database for MySQL (Flexible Server) | Versionado inmutable; caché en memoria |
| **Payment (100% Online)** | `id`, `stayId` o `bookingId`, `monto`, `moneda`, `método` (*apple_pay*, *card_token*, etc.), `pspReference`, `idempotencyKey` | Azure Database for MySQL (Flexible Server) | Cero almacenamiento de PAN/CVV; clave de idempotencia única; TDE |
| **Incident** | `id`, `carrilId`, `tipoIncidencia`, `estado`, `operadorId`, `causaResolución`, `eventoId` | Azure Database for MySQL (Flexible Server) | TDE en reposo |
| **SecurityAlert** | `id`, `eventoId`, `referenciaReporte`, `dictamen`, `comentario`, `oficialId` | Azure Database for MySQL (Flexible Server) | `comentario` y `referenciaReporte` cifrados a nivel de columna con **AES-256-GCM**; TDE |
| **Device** | `id`, `carril`, `versiónAgente`, `estadoConexión`, `últimoLatido` | Azure Database for MySQL & IoT Hub Device Twin | Autenticación mTLS mediante certificados X.509 |
| **ListEntry** | `placa`, `tipoLista` (*blanca / negra*), `justificación`, `vigencia` | Azure Database for MySQL (Flexible Server) | Replicado a SQLite local en edge cada 5 min |
| **AuditEntry** | `id`, `actorId`, `acción`, `recurso`, `payloadPrevio`, `payloadNuevo`, `ip`, `timestamp` | Azure Database for MySQL (Flexible Server) | Tabla *append-only* (solo anexar); datos sensibles anonimizados o cifrados |
| **Occupancy** | `zoneId`, `cajonesOcupados`, `cajonesReservados`, `cajonesDisponibles` | Redis Cache (persistencia periódica en MySQL) | Estructuras atómicas en memoria para alta concurrencia |

### 10.1 Estrategia de acceso a datos y ORM escalable (.NET + MySQL)
- **Entity Framework Core 10 con Pomelo MySQL:**
  - Integración mediante `Pomelo.EntityFrameworkCore.MySql` configurado con collation `utf8mb4_0900_ai_ci` para soporte total de caracteres internacionales y emojis.
  - Pool de conexiones optimizado mediante `MySqlConnector` para reutilizar sockets y evitar la sobrecarga de handshake TLS en cada petición.
  - Migraciones de base de datos declarativas (`dotnet ef migrations`) integradas y auditadas en los pipelines de despliegue automatizado.
- **Consultas de solo lectura de alto rendimiento:**
  - Uso estricto de `.AsNoTracking()` en todas las consultas de consulta y reportes para anular el seguimiento de cambios (*Change Tracker*) y reducir drásticamente el consumo de memoria del backend.
  - Enrutamiento de consultas pesadas a las **Read Replicas** de Azure Database for MySQL mediante un contexto segregado (`ReadOnlyDbContext`), manteniendo el nodo primario libre para operaciones críticas de acceso y cobro.
  - Paginación basada en claves (*Keyset Pagination*) en listados de gran volumen para mantener tiempos de respuesta en $O(1)$ sin incurrir en degradación por `OFFSET` elevado.

### Servicios de aplicación (Interfaces de dominio)

| Servicio | Responsabilidad técnica |
| :--- | :--- |
| `IAccessDecisionService` | Aplica reglas de negocio en accesos y emite respuesta de apertura/rechazo |
| `IPlateNormalizer` | Normaliza sintaxis de placas mexicanas e internacionales |
| `IBookingService` | Gestión de disponibilidad, altas, reprogramaciones y cancelaciones |
| `IPricingService` | Cálculo exacto de tarifas y aplicación de periodos de gracia |
| `IPaymentService` | Orquestación de cobros en línea con pasarela (Apple Pay, tokens, 3DS, reembolsos) |
| `IPoliceLookupClient` | Cliente resiliente para consulta desacoplada a la API policial con caché |
| `IOccupancyService` | Gestión de inventario de plazas en Redis y publicación en vivo vía SignalR |
| `INotificationService` | Envío de notificaciones push (Apple APNs / Firebase), correos y SMS |
| `IAuditService` | Registro inalterable de operaciones críticas y consultas de seguridad |
| `IDeviceService` | Monitoreo de telemetría, despliegue de comandos OTA y rotación de credenciales |
| `ICryptographyService` | Cifrado y descifrado de campos sensibles con AES-256-GCM y hashing irreversible Argon2id |

### Esquema del evento edge: `access.detected`
```json
{
  "eventId": "0b8f9c6e-4a1d-4c1f-9d71-3f7a5e2c1b10",
  "type": "access.detected",
  "version": 1,
  "terminalId": "AGU-T1",
  "deviceId": "pi-entry-01",
  "lane": "entry",
  "occurredAt": "2026-10-16T10:42:07.123Z",
  "plate": {
    "raw": "ABC-123-D",
    "normalized": "ABC123D",
    "confidence": 0.96,
    "engine": "azure-vision"
  },
  "evidence": {
    "frameUri": "blob://evidence/AGU-T1/2026/10/16/0b8f9c6e.jpg"
  },
  "agentVersion": "1.4.2"
}
```

---

## 11. Seguridad, privacidad e integración policial

La arquitectura adopta el modelo de **Cero Confianza (*Zero Trust*)** y garantiza que la tecnología esté siempre al servicio de la supervisión humana, protegiendo rigurosamente la privacidad y la información confidencial.

### 11.1 Cifrado integral de información sensible
Para garantizar la confidencialidad e integridad de todos los datos en el ciclo de vida del sistema:
- **Cifrado en tránsito:** Toda comunicación externa e interna utiliza **TLS 1.3** con suites criptográficas robustas (ej. `TLS_AES_256_GCM_SHA384`). Se aplica HSTS estricto para forzar canales seguros. La comunicación entre las Raspberry Pi y Azure IoT Hub/APIs se protege con mTLS (certificados X.509 de cliente y servidor).
- **Cifrado en reposo (Base de datos):** En **Azure Database for MySQL (Flexible Server)**, los datos de almacenamiento, archivos temporales y copias de seguridad se cifran automáticamente mediante Transparent Data Encryption (TDE) con claves administradas en **Azure Key Vault** con rotación programada.
- **Cifrado a nivel de aplicación (Column-Level Encryption):** Toda información personal identificable (PII) o altamente confidencial (teléfonos de clientes, correos en logs, dictámenes de seguridad) se cifra con **AES-256-GCM** en el backend antes de escribir en MySQL, garantizando que incluso un volcado no autorizado de la base de datos sea ilegible sin la clave maestra de Key Vault.
- **Cifrado en el dispositivo móvil (React Native):** Los tokens JWT, credenciales temporales y metadatos sensibles se almacenan en el enclave criptográfico seguro del hardware móvil (iOS Keychain / Android Keystore con respaldo TEE) a través de `expo-secure-store`.
- **Cifrado de evidencias fotográficas:** Las imágenes de captura vehicular en Azure Blob Storage cuentan con cifrado de servidor (SSE AES-256) y solo pueden ser consultadas mediante firmas de acceso compartido (**SAS Tokens**) de vigencia efímera (15 minutos).

### 11.2 Gestión de contraseñas: Irreversibilidad y no descifrabilidad
El sistema garantiza que **es imposible descifrar las contraseñas** almacenadas:
- **Ausencia de mecanismos de descifrado:** Las contraseñas jamás se almacenan en texto claro ni con algoritmos de cifrado simétrico o asimétrico que permitan reversión. No existe clave de desencriptación ni función inversa en el software.
- **Función hash criptográfica unidireccional (Argon2id):** Se utiliza el algoritmo **Argon2id** (reconocido como el estándar más robusto frente a ataques por GPU, FPGA y ASICs), parametrizado con un costo de memoria de $64\text{ MB}$, al menos $3$ iteraciones y $4$ hilos en paralelo.
- **Salt criptográfico único por cuenta:** Cada registro incorpora una sal aleatoria e irrepetible de **128 bits** generada mediante un generador criptográficamente seguro (`CSPRNG`). Esto neutraliza ataques de tablas de precomputación (*Rainbow Tables*) y ataques de colisión masiva.
- **Verificación en tiempo constante:** La comparación del hash durante el login se realiza mediante algoritmos de tiempo constante (`CryptographicOperations.FixedTimeEquals`), impidiendo la deducción de caracteres mediante ataques de canal lateral basados en tiempo (*Timing Attacks*).
- **Protección contra ataques de fuerza bruta:** Limitación estricta de solicitudes (máximo 5 peticiones por minuto por IP/cuenta), bloqueo progresivo de cuenta y validación contra listas de contraseñas vulneradas.

### Integración policial (Police Gateway)
1. **Convenio institucional obligatorio:** Previo a la salida a producción, se formaliza el acuerdo legal que estipula alcances y responsabilidades. El desarrollo se ejecuta contra un simulador de contratos fidedignos.
2. **Aislamiento en DMZ:** El Police Gateway es un microservicio protegido en red privada, único con acceso mTLS a las interfaces oficiales.
3. **Minimización estricta:** Solo se envía la placa y se recibe un indicador booleano con clave de reporte (sin acceder a expedientes judiciales ni almacenar datos de terceros).
4. **Caché controlada de 15 minutos:** Impide saturación de la autoridad ante vehículos en maniobra.
5. **Decisión humana irremplazable:** Cualquier coincidencia genera una alerta en la consola del personal de seguridad. La pluma vehicular nunca se bloquea automáticamente por software.

### Matriz de riesgos y controles de mitigación

| Amenaza | Control de mitigación |
| :--- | :--- |
| **Suplantación de hardware edge** | Certificados X.509 únicos por Raspberry Pi con aprovisionamiento DPS y red VLAN aislada con mTLS |
| **Robo de sesión administrativa** | Autenticación Entra ID con MFA obligatorio, caducidad de tokens en 8 h y confirmación biométrica para acciones clave |
| **Exfiltración de base de datos** | Azure Database for MySQL con TDE + cifrado de columna AES-256-GCM para PII + llaves en Azure Key Vault |
| **Ataque de fuerza bruta a contraseñas** | Imposibilidad de descifrado: hashing unidireccional con **Argon2id** + salt de 128 bits + comparación en tiempo constante + Rate Limiting |
| **Fraude en cobros digitales** | Claves de idempotencia obligatorias en cada solicitud de cobro, conciliación diaria y 3DS 2.0 (cero almacenamiento de tarjetas) |
| **Enumeración de placas en web** | Limitador de tasa (*Rate Limiting*) por IP, respuestas de error idénticas y CAPTCHA adaptativo |
| **Manipulación de bitácoras** | Almacenamiento append-only en MySQL, réplica inmutable WORM en Azure Blob con bloqueo legal |
| **Fuga de fotografías vehiculares** | Contenedores Azure Blob cerrados, generación de firmas SAS temporales (15 min) y cifrado SSE AES-256 |
| **Falsos positivos de lectura** | Umbrales de confianza calibrados ($\ge 90\%$), revisión manual en incidencias y dictamen humano en seguridad |
| **Ataques a dependencias de software** | Auditoría continua en CI/CD con Dependabot y análisis estático con GitHub CodeQL |

### Privacidad y cumplimiento normativo (LFPDPPP)
- **Aviso de privacidad claro:** Información accesible en la app móvil y síntesis legible en la señalética de acceso físico.
- **Flujos ARCO integrados:** El usuario puede solicitar la descarga o supresión de sus datos personales directamente desde la app en las pantallas C-31 y A-24.
- **Tiempos de retención de información:**

| Tipo de registro | Plazo de retención | Justificación |
| :--- | :--- | :--- |
| Fotografías de matrículas sin incidencia | 30 días naturales | Aclaraciones y soporte de cobro |
| Metadatos de acceso y estancia | 12 meses | Estadísticas de capacidad y planeación |
| Registros contables y pagos digitales | 5 años | Obligaciones fiscales y auditoría bancaria |
| Alertas policiales descartadas (falsos positivos) | 90 días | Calibración y mejora continua del OCR |
| Alertas policiales confirmadas | Según disposición judicial | Cadena de custodia legal |
| Bitácora inmutable de auditoría | 24 meses | Trazabilidad y seguridad informática |

---

## 12. Calidad, DevOps y observabilidad

La estabilidad del sistema se respalda en pruebas automatizadas y despliegues controlados con observabilidad de extremo a extremo.

### Pirámide de pruebas automatizadas

| Nivel | Alcance evaluado | Herramientas | Meta de cobertura |
| :--- | :--- | :--- | :--- |
| **Unitarias** | Reglas de acceso, algoritmos tarifarios, normalización de placas | xUnit (.NET), pytest (Python), Jest (React Native) | $\ge 80\%$ en lógica de negocio |
| **Integración** | Endpoints de API contra motores de persistencia reales | WebApplicationFactory, Testcontainers | 100% de rutas de API |
| **Contrato** | Validación OpenAPI contra clientes TypeScript y esquemas JSON | Schemathesis, validadores JSON Schema | 0 breaking changes |
| **Edge** | Simulación de máquina de estados con hardware virtual | pytest con simuladores de lazo y cámara | Cobertura total de transiciones |
| **End-to-End (E2E)** | Flujo integral: reserva, llegada, pago 1-tap y salida | Playwright (Web), Maestro (Móvil) | 10 flujos críticos |
| **Precisión OCR** | Banco de imágenes reales etiquetadas (día, noche, lluvia) | Pipeline de validación continua | Superar las metas de la sección 1 |
| **Pruebas de carga** | Concurrencia masiva en accesos y picos de salida simultáneos | k6 | 3x sobre la demanda máxima proyectada |
| **Seguridad (SAST/DAST)** | Escaneo estático de código, secretos y dependencias | CodeQL, Dependabot, pentest externo | 0 vulnerabilidades críticas |

### Ciclo de integración y despliegue continuo (CI/CD)
- **Entornos:** `dev` (con simulador de carril), `test` (carril físico de pruebas) y `prod` (carriles reales del aeropuerto).
- **Pipelines en GitHub Actions:**
  - **Backend:** Compilación, ejecución de pruebas, contenedor Docker y despliegue Blue-Green en Azure Container Apps.
  - **App Cliente & Web:** Compilación en Expo EAS Build, publicación web automatizada y actualizaciones OTA (*Over-The-Air*) con EAS Update.
  - **Agente Edge:** Empaquetado firmado digitalmente y distribución controlada vía Azure IoT Hub Device Update.
- **Infraestructura como Código (IaC):** Plantillas de Bicep/Terraform versionadas en el repositorio.

### Objetivos de nivel de servicio (SLOs)

| Métrica | Objetivo de servicio |
| :--- | :--- |
| **Decisión de acceso en la nube (p95)** | $\le 1$ segundo |
| **Tiempo total de detección a apertura física** | $\le 3$ segundos |
| **Disponibilidad mensual del carril** | $99.9\%$ (garantizado con respaldo en modo degradado) |
| **Tasa de éxito en pagos en línea** | $\ge 98\%$ |
| **Latencia de entrega de alertas al panel** | $\le 5$ segundos |
| **Pérdida de eventos en fallos de red** | $0\%$ (garantizado por cola FIFO local en SQLite) |

### Procedimientos operativos de contingencia (Runbooks)
1. **RB-01:** Pluma trabada mecánicamente o sin respuesta del relé.
2. **RB-02:** Suciedad en lente de cámara, desenfoque o pérdida de señal de video.
3. **RB-03:** Pérdida prolongada de internet en el carril (validación de modo degradado).
4. **RB-04:** Interrupción temporal en el proveedor de pagos (PSP) y manejo de transacciones en cola.
5. **RB-05:** Protocolo de atención y disculpa ante falso positivo de reporte policial.
6. **RB-06:** Ejecución de solicitud de borrado de datos (Derechos ARCO).
7. **RB-07:** Recuperación ante desastres (DRP) y restauración de bases de datos desde réplicas.
