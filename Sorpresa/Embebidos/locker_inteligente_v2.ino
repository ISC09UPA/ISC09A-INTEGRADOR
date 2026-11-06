/*
  PROYECTO SORPRESA - Locker Inteligente de Renta (ISC09A)
  Arduino UNO + Proteus  |  VERSION 2

  Conexiones:
    Servo (cerradura)          -> D9
    Switch puerta (magnetico)  -> D2 a GND   (cerrado = puerta cerrada)
    LED verde / amarillo / rojo-> D3 / D4 / D5 (cada uno con 220 ohm a GND)
    BOTON DE CIERRE            -> D6 a GND   (NUEVO)
    HC-SR04 TRIG / ECHO        -> D7 / D8
    Potenciometro (peso)       -> A0
    Virtual Terminal           -> RXD a D1, TXD a D0, 9600 baud

  Comandos (terminal, sin Enter):
    R = rentar | A = abrir con QR | C = cerrar | F = finalizar | S = estado
  El boton D6 hace lo mismo que el comando C.
*/

#include <Servo.h>

Servo cerradura;

// ---------- Pines ----------
const int PIN_SERVO    = 9;
const int PIN_PUERTA   = 2;
const int PIN_VERDE    = 3;
const int PIN_AMARILLO = 4;
const int PIN_ROJO     = 5;
const int PIN_BOTON    = 6;
const int TRIG         = 7;
const int ECHO         = 8;
const int PIN_PESO     = A0;

// ---------- Parametros ----------
const float PESO_MAX_KG_SENSOR = 50.0;   // rango de la celda de carga
const float LIMITE_RENTA_KG    = 20.0;   // limite acordado con el usuario
const float TOLERANCIA_KG      = 1.0;    // variacion permitida con locker cerrado
const float PESO_VACIO_KG      = 0.5;    // menos de esto = vacio
const float DIST_VACIO_CM      = 20.0;   // fondo mas lejos que esto = vacio
const bool  USAR_SONAR         = false;  // true para exigir tambien el sonar

const unsigned long TIEMPO_RENTA = 60000UL;
const unsigned long AVISO_ANTES  = 15000UL;
const unsigned long PERIODO_MSG  = 1000UL;
const unsigned long REBOTE_MS    = 50UL;

// ---------- Estado ----------
bool rentado = false;      // hay una renta activa
bool abierto = false;      // la cerradura esta abierta (servo a 90)
bool armado  = false;      // el usuario cerro y se vigila el peso/puerta
unsigned long inicioRenta = 0;
unsigned long ultimoMsg = 0;
float pesoInicial = 0;

// ---------- Utilidades ----------
float leerPeso() {
  return analogRead(PIN_PESO) * PESO_MAX_KG_SENSOR / 1023.0;
}

float leerDistanciaCm() {
  digitalWrite(TRIG, LOW);
  delayMicroseconds(2);
  digitalWrite(TRIG, HIGH);
  delayMicroseconds(10);
  digitalWrite(TRIG, LOW);
  long dur = pulseIn(ECHO, HIGH, 30000);
  if (dur == 0) return 999.0;            // sin eco: nada cerca
  return dur * 0.034 / 2.0;
}

bool puertaCerrada() { return digitalRead(PIN_PUERTA) == LOW; }

bool lockerVacio() {
  bool pesoOk = leerPeso() < PESO_VACIO_KG;
  if (!USAR_SONAR) return pesoOk;
  return pesoOk && leerDistanciaCm() > DIST_VACIO_CM;
}

void abrir() {
  cerradura.write(90);
  abierto = true;
  armado = false;
}

void cerrar() {
  cerradura.write(0);
  abierto = false;
}

void leds(bool verde, bool amarillo, bool rojo) {
  digitalWrite(PIN_VERDE, verde);
  digitalWrite(PIN_AMARILLO, amarillo);
  digitalWrite(PIN_ROJO, rojo);
}

// Devuelve true solo en el instante en que se presiona el boton (con antirrebote)
bool botonPresionado() {
  static bool estable = false;
  static unsigned long ultimoCambio = 0;
  bool actual = (digitalRead(PIN_BOTON) == LOW);
  if (actual != estable && millis() - ultimoCambio > REBOTE_MS) {
    ultimoCambio = millis();
    estable = actual;
    return actual;
  }
  return false;
}

// ---------- Acciones ----------
void rentar() {
  if (rentado) {
    Serial.println("El locker ya esta rentado.");
    return;
  }
  rentado = true;
  armado = false;
  inicioRenta = millis();
  pesoInicial = 0;
  leds(false, false, false);
  Serial.println("Renta iniciada. Use A para abrir con QR.");
}

void abrirConQR() {
  if (!rentado) {
    Serial.println("Primero rente el locker (R).");
  } else if (millis() - inicioRenta > TIEMPO_RENTA) {
    Serial.println("ACCESO BLOQUEADO: tiempo excedido. Se aplica cargo extra.");
  } else if (abierto) {
    Serial.println("El locker ya esta abierto.");
  } else {
    abrir();
    Serial.println("QR valido. Locker abierto.");
  }
}

void intentarCerrar() {
  if (!rentado) {
    Serial.println("No hay renta activa.");
    return;
  }
  if (!abierto) {
    Serial.println("El locker ya esta cerrado.");
    return;
  }
  if (!puertaCerrada()) {
    Serial.println("Cierre la puerta (switch) antes de bloquear.");
    return;
  }
  float peso = leerPeso();
  if (peso > LIMITE_RENTA_KG) {
    Serial.println("No se puede cerrar: peso sobre el limite. Retire objetos.");
    return;
  }
  cerrar();
  pesoInicial = peso;
  armado = true;
  Serial.print("Locker cerrado. Peso inicial: ");
  Serial.print(peso);
  Serial.println(" kg");
  if (peso < PESO_VACIO_KG) {
    Serial.println("AVISO: el locker esta vacio.");
  }
}

void finalizar() {
  if (!rentado) {
    Serial.println("No hay renta activa.");
    return;
  }
  abrir();
  delay(500);
  if (lockerVacio()) {
    rentado = false;
    cerrar();
    leds(true, false, false);
    Serial.println("Locker vacio. Renta finalizada, locker libre.");
  } else {
    Serial.println("ALERTA: aun hay objetos en el locker. Retirelos y pulse F.");
  }
}

void mostrarEstado() {
  Serial.print("Peso: ");
  Serial.print(leerPeso());
  Serial.print(" kg | Puerta: ");
  Serial.print(puertaCerrada() ? "CERRADA" : "ABIERTA");
  Serial.print(" | Cerradura: ");
  Serial.print(abierto ? "ABIERTA" : "BLOQUEADA");
  Serial.print(" | Distancia: ");
  Serial.print(leerDistanciaCm());
  Serial.print(" cm | Renta: ");
  Serial.println(rentado ? "ACTIVA" : "LIBRE");
}

// ---------- Supervision durante la renta ----------
void supervisar() {
  unsigned long transcurrido = millis() - inicioRenta;
  float peso = leerPeso();
  bool alerta = false;
  bool aviso = false;
  bool imprimir = (millis() - ultimoMsg >= PERIODO_MSG);
  if (imprimir) ultimoMsg = millis();

  // Tiempo
  if (transcurrido > TIEMPO_RENTA) {
    alerta = true;
    if (imprimir) Serial.println("ALERTA: tiempo excedido. Cargo extra / acceso bloqueado.");
  } else if (transcurrido > TIEMPO_RENTA - AVISO_ANTES) {
    aviso = true;
    if (imprimir) {
      Serial.print("AVISO: quedan ");
      Serial.print((TIEMPO_RENTA - transcurrido) / 1000);
      Serial.println(" s de renta.");
    }
  }

  // Sobrepeso (aplica abierto o cerrado)
  if (peso > LIMITE_RENTA_KG) {
    alerta = true;
    if (imprimir) {
      Serial.print("ALERTA: peso sobre el limite (");
      Serial.print(peso);
      Serial.println(" kg).");
    }
  }

  // Solo si el usuario ya cerro (armado)
  if (armado) {
    if (fabs(peso - pesoInicial) > TOLERANCIA_KG) {
      alerta = true;
      if (imprimir) Serial.println("ALERTA: el peso cambio con el locker cerrado.");
    }
    if (!puertaCerrada()) {
      alerta = true;
      if (imprimir) Serial.println("ALERTA: puerta abierta sin autorizacion.");
    }
  }

  if (alerta)     leds(false, false, true);
  else if (aviso) leds(false, true, false);
  else            leds(false, false, false);
}

// ---------- Arduino ----------
void setup() {
  Serial.begin(9600);
  cerradura.attach(PIN_SERVO);
  pinMode(PIN_PUERTA, INPUT_PULLUP);
  pinMode(PIN_BOTON, INPUT_PULLUP);
  pinMode(PIN_VERDE, OUTPUT);
  pinMode(PIN_AMARILLO, OUTPUT);
  pinMode(PIN_ROJO, OUTPUT);
  pinMode(TRIG, OUTPUT);
  pinMode(ECHO, INPUT);

  cerrar();
  leds(true, false, false);
  Serial.println("Locker libre. Comandos: R A C F S | Boton D6 = cerrar");
}

void loop() {
  while (Serial.available()) {
    char c = toupper(Serial.read());
    if      (c == 'R') rentar();
    else if (c == 'A') abrirConQR();
    else if (c == 'C') intentarCerrar();
    else if (c == 'F') finalizar();
    else if (c == 'S') mostrarEstado();
  }

  if (botonPresionado()) {
    intentarCerrar();
  }

  if (rentado) supervisar();

  delay(20);
}
