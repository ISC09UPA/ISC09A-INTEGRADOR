let contador = 0;

function nuevo(prefijo: string): string {
  contador += 1;
  return `${prefijo}-${Date.now()}-${contador}`;
}

export const nuevoIdEvento = () => nuevo('e');
export const nuevoIdRiego = () => nuevo('r');
export const nuevoIdRegla = () => nuevo('rr');
export const nuevoIdEscaneo = () => nuevo('s');
export const nuevoIdUsuario = () => nuevo('u');