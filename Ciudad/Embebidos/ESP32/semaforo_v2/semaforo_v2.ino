// ============================================================
// Cruce Inteligente - Firmware ESP32 v2
// Ciclo automatico (4 accesos + botones peatonales) + modo manual
// + prioridad de ambulancia, controlados por comandos seriales
// que manda el backend .NET desde la Raspberry Pi.
//
// Protocolo (texto plano, una linea terminada en \n):
//   MODO:AUTO
//   MODO:MANUAL
//   SET:<acceso 1-4>:VERDE|AMARILLO|ROJO   (solo valido en modo manual)
//   AMBULANCIA:<acceso 1-4>
//   AMBULANCIA:FIN
//   TRAFICO:<v1>,<v2>,<v3>,<v4>   (vehiculos por acceso, desde la camara)
//
// Respuestas del ESP32 hacia el backend:
//   OK:<comando> / ERR:<motivo>       despues de cada comando
//   EST:<acceso 0-4>:<fase>:<ms>      en CADA cambio de fase del ciclo
//                                     automatico (acceso 0 = todos en rojo).
//                                     fase = TODOS_ROJO | VERDE | PARPADEO |
//                                            AMARILLO | ROJO_FIN
//                                     ms   = duracion de esa fase
//   El backend usa EST: para que la app muestre el cruce en vivo.
//
// Accesos: 1 = Norte, 2 = Sur, 3 = Este, 4 = Oeste
// ============================================================

// ---- Pines de salida ----
#define VERDE1 26
#define AMARILLO1 25
#define ROJO1 33
#define VERDE2 32
#define AMARILLO2 14   // antes 35: GPIO34-39 del ESP32 son solo entrada
#define ROJO2 27       // antes 34
#define VERDE3 23
#define AMARILLO3 22
#define ROJO3 21
#define VERDE4 19
#define AMARILLO4 18
#define ROJO4 17

// ---- Botones peatonales ----
#define BOTON1 4
#define BOTON2 5

int PIN_VERDE[4]    = {VERDE1, VERDE2, VERDE3, VERDE4};
int PIN_AMARILLO[4] = {AMARILLO1, AMARILLO2, AMARILLO3, AMARILLO4};
int PIN_ROJO[4]     = {ROJO1, ROJO2, ROJO3, ROJO4};
int PIN_BOTON[4]    = {BOTON1, BOTON2, -1, -1}; // -1 = boton no conectado aun

enum Modo { MODO_AUTO, MODO_MANUAL, MODO_AMBULANCIA };
Modo modo = MODO_AUTO;

enum EstadoAuto { LAP_INICIO, VERDE, PARPADEO, AMARILLO, ROJO_FIN };
EstadoAuto estadoAuto = LAP_INICIO;
int accesoActual = 0;
unsigned long tEstado = 0;
int parpadeoContador = 0;
bool parpadeoEncendido = false;

const unsigned long DUR_LAP_INICIO = 500;
const unsigned long DUR_VERDE = 2500;       // valor por defecto, sin datos de camara
const unsigned long DUR_VERDE_MIN = 1500;   // piso: nunca menos que esto, aunque haya 0 vehiculos
const unsigned long DUR_VERDE_MAX = 5000;   // techo: nunca mas que esto, por seguridad del resto de accesos
const unsigned long DUR_PARPADEO_FASE = 200;
const unsigned long DUR_AMARILLO = 1000;
const unsigned long DUR_ROJO_FIN = 500;

// Conteo de vehiculos por acceso, enviado por el backend con TRAFICO:
// (lo calcula la camara + OpenCV, ver Embebidos/RaspberryPi/vision). Mientras no
// llegue ningun TRAFICO, queda en 0 y se usa el tiempo fijo DUR_VERDE.
int conteoTrafico[4] = {0, 0, 0, 0};
unsigned long duracionVerdeActual = DUR_VERDE;

String bufferSerial = "";

// Prioridad por densidad de trafico: cada vehiculo detectado por la camara
// en ESTE acceso suma tiempo extra de verde, acotado entre DUR_VERDE_MIN y
// DUR_VERDE_MAX. Si no hay datos de camara (conteo en 0) regresa DUR_VERDE,
// el mismo tiempo fijo que ya se usaba antes de tener camara.
unsigned long calcularDuracionVerde(int acc) {
  if (conteoTrafico[acc] <= 0) return DUR_VERDE;
  long extraPorVehiculo = 300; // ms de verde extra por cada vehiculo contado
  long duracion = (long)DUR_VERDE_MIN + (long)conteoTrafico[acc] * extraPorVehiculo;
  if (duracion < (long)DUR_VERDE_MIN) duracion = DUR_VERDE_MIN;
  if (duracion > (long)DUR_VERDE_MAX) duracion = DUR_VERDE_MAX;
  return (unsigned long)duracion;
}

// Linea EST: que lee el backend (SerialSemaforoController) para saber en
// que fase va el ciclo sin tener que preguntar.
void reportarEstado(const char* fase, int acceso1a4, unsigned long duracionMs) {
  Serial.print("EST:");
  Serial.print(acceso1a4);
  Serial.print(":");
  Serial.print(fase);
  Serial.print(":");
  Serial.println(duracionMs);
}

void apagarTodosPines() {
  for (int i = 0; i < 4; i++) {
    digitalWrite(PIN_VERDE[i], LOW);
    digitalWrite(PIN_AMARILLO[i], LOW);
    digitalWrite(PIN_ROJO[i], LOW);
  }
}

void iniciarLapInicio() {
  apagarTodosPines();
  for (int i = 0; i < 4; i++) digitalWrite(PIN_ROJO[i], HIGH);
  // cada vuelta (y cada regreso a AUTO) empieza otra vez por el acceso 1;
  // sin esto, despues de la primera vuelta el ciclo se quedaba en el acceso 4
  accesoActual = 0;
  estadoAuto = LAP_INICIO;
  tEstado = millis();
  reportarEstado("TODOS_ROJO", 0, DUR_LAP_INICIO);
}

void iniciarVerde(int acc) {
  accesoActual = acc;
  duracionVerdeActual = calcularDuracionVerde(acc);
  digitalWrite(PIN_ROJO[acc], LOW);
  digitalWrite(PIN_VERDE[acc], HIGH);
  estadoAuto = VERDE;
  tEstado = millis();
  reportarEstado("VERDE", acc + 1, duracionVerdeActual);
}

void iniciarParpadeo() {
  parpadeoContador = 0;
  parpadeoEncendido = true;
  estadoAuto = PARPADEO;
  tEstado = millis();
  reportarEstado("PARPADEO", accesoActual + 1, DUR_PARPADEO_FASE * 6);
}

void iniciarAmarillo() {
  digitalWrite(PIN_VERDE[accesoActual], LOW);
  digitalWrite(PIN_AMARILLO[accesoActual], HIGH);
  estadoAuto = AMARILLO;
  tEstado = millis();
  reportarEstado("AMARILLO", accesoActual + 1, DUR_AMARILLO);
}

void iniciarRojoFin() {
  digitalWrite(PIN_AMARILLO[accesoActual], LOW);
  digitalWrite(PIN_ROJO[accesoActual], HIGH);
  estadoAuto = ROJO_FIN;
  tEstado = millis();
  reportarEstado("ROJO_FIN", accesoActual + 1, DUR_ROJO_FIN);
}

void actualizarAuto() {
  unsigned long ahora = millis();
  switch (estadoAuto) {
    case LAP_INICIO:
      if (ahora - tEstado >= DUR_LAP_INICIO) iniciarVerde(accesoActual);
      break;
    case VERDE: {
      int boton = PIN_BOTON[accesoActual];
      bool presionado = (boton != -1 && digitalRead(boton) == LOW);
      // duracionVerdeActual: fijo (DUR_VERDE) o extendido segun la
      // densidad de trafico reportada por la camara (ver calcularDuracionVerde)
      if (presionado || ahora - tEstado >= duracionVerdeActual) iniciarParpadeo();
      break;
    }
    case PARPADEO:
      if (ahora - tEstado >= DUR_PARPADEO_FASE) {
        parpadeoEncendido = !parpadeoEncendido;
        digitalWrite(PIN_VERDE[accesoActual], parpadeoEncendido ? HIGH : LOW);
        tEstado = ahora;
        parpadeoContador++;
        if (parpadeoContador >= 6) iniciarAmarillo();
      }
      break;
    case AMARILLO:
      if (ahora - tEstado >= DUR_AMARILLO) iniciarRojoFin();
      break;
    case ROJO_FIN:
      if (ahora - tEstado >= DUR_ROJO_FIN) {
        int siguiente = (accesoActual + 1) % 4;
        if (siguiente == 0) iniciarLapInicio();
        else iniciarVerde(siguiente);
      }
      break;
  }
}

void procesarComando(String cmd) {
  cmd.trim();
  if (cmd.length() == 0) return;

  if (cmd == "MODO:AUTO") {
    modo = MODO_AUTO;
    iniciarLapInicio();
    Serial.println("OK:MODO:AUTO");

  } else if (cmd == "MODO:MANUAL") {
    modo = MODO_MANUAL;
    apagarTodosPines();
    Serial.println("OK:MODO:MANUAL");

  } else if (cmd.startsWith("SET:")) {
    if (modo != MODO_MANUAL) { Serial.println("ERR:no esta en modo manual"); return; }
    int p1 = cmd.indexOf(':', 4);
    if (p1 == -1) { Serial.println("ERR:formato"); return; }
    int acc = cmd.substring(4, p1).toInt() - 1;
    String color = cmd.substring(p1 + 1);
    if (acc < 0 || acc > 3) { Serial.println("ERR:acceso invalido"); return; }
    digitalWrite(PIN_VERDE[acc], LOW);
    digitalWrite(PIN_AMARILLO[acc], LOW);
    digitalWrite(PIN_ROJO[acc], LOW);
    if (color == "VERDE") digitalWrite(PIN_VERDE[acc], HIGH);
    else if (color == "AMARILLO") digitalWrite(PIN_AMARILLO[acc], HIGH);
    else if (color == "ROJO") digitalWrite(PIN_ROJO[acc], HIGH);
    Serial.println("OK:SET");

  } else if (cmd.startsWith("AMBULANCIA:")) {
    String val = cmd.substring(11);
    if (val == "FIN") {
      modo = MODO_AUTO;
      iniciarLapInicio();
      Serial.println("OK:AMBULANCIA:FIN");
    } else {
      int acc = val.toInt() - 1;
      if (acc < 0 || acc > 3) { Serial.println("ERR:acceso invalido"); return; }
      modo = MODO_AMBULANCIA;
      apagarTodosPines();
      for (int i = 0; i < 4; i++) digitalWrite(PIN_ROJO[i], HIGH);
      digitalWrite(PIN_ROJO[acc], LOW);
      digitalWrite(PIN_VERDE[acc], HIGH);
      Serial.println("OK:AMBULANCIA");
    }
  } else if (cmd.startsWith("TRAFICO:")) {
    String valores = cmd.substring(8);
    int nuevoConteo[4];
    int inicio = 0;
    bool valido = true;
    for (int i = 0; i < 4; i++) {
      int coma = (i < 3) ? valores.indexOf(',', inicio) : valores.length();
      if (coma == -1) { valido = false; break; }
      String parte = valores.substring(inicio, coma);
      parte.trim();
      if (parte.length() == 0) { valido = false; break; }
      nuevoConteo[i] = parte.toInt();
      if (nuevoConteo[i] < 0) { valido = false; break; }
      inicio = coma + 1;
    }
    if (!valido) {
      Serial.println("ERR:formato (se esperan 4 enteros separados por coma)");
      return;
    }
    for (int i = 0; i < 4; i++) conteoTrafico[i] = nuevoConteo[i];
    Serial.println("OK:TRAFICO");

  } else {
    Serial.println("ERR:comando no reconocido");
  }
}

void leerSerial() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n') {
      procesarComando(bufferSerial);
      bufferSerial = "";
    } else if (c != '\r') {
      bufferSerial += c;
    }
  }
}

void setup() {
  Serial.begin(115200);
  for (int i = 0; i < 4; i++) {
    pinMode(PIN_VERDE[i], OUTPUT);
    pinMode(PIN_AMARILLO[i], OUTPUT);
    pinMode(PIN_ROJO[i], OUTPUT);
    if (PIN_BOTON[i] != -1) pinMode(PIN_BOTON[i], INPUT_PULLUP);
  }
  iniciarLapInicio();
}

void loop() {
  leerSerial();
  if (modo == MODO_AUTO) actualizarAuto();
  // en MANUAL y AMBULANCIA no hay nada que actualizar por tiempo,
  // los pines ya quedaron puestos por el comando que llego
}
