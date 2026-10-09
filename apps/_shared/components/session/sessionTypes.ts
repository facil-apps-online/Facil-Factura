// Tipos y ayudas comunes de la sesión de los cuatro portales (tenant, cliente, developers y superadmin).

// Lo mínimo que se usa de axios, para no depender de su tipo desde el código compartido.
export interface SessionHttp {
  get(url: string): Promise<{ data: any }>;
  put(url: string, body?: any): Promise<{ data: any }>;
  post(url: string, body?: any): Promise<{ data: any }>;
}

// Resumen del usuario que muestra el menú del avatar y la página de perfil (GET {basePath}/me).
export interface AccountMe {
  name: string;
  displayName: string;
  email: string;
  role: string;
  organization: string | null;
  sessionMinutes: number;
  allowedSessionMinutes: number[];
  absoluteCapHours: number;
}

// Claims propios del token de sesión (ver SessionPolicy en el backend): emisión, vencimiento, inicio de la sesión y tope absoluto.
export interface SessionClaims {
  exp: number;
  iat: number;
  sst: number;
  cap: number;
}

// Lee el vencimiento y el tope del token sin validarlo (eso lo hace el servidor): solo sirve para saber cuándo avisar.
export function readSessionClaims(token: string | null): SessionClaims | null {
  if (!token) return null;
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
    const { exp, iat, sst, cap } = payload;
    return [exp, iat, sst, cap].every(v => typeof v === 'number') ? { exp, iat, sst, cap } : null;
  } catch {
    return null;
  }
}

// "0:45", "12:03", "1:02:07".
export function formatCountdown(ms: number): string {
  const total = Math.max(0, Math.ceil(ms / 1000));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  const pad = (n: number) => String(n).padStart(2, '0');
  return h > 0 ? `${h}:${pad(m)}:${pad(s)}` : `${m}:${pad(s)}`;
}

export function minutesLabel(minutes: number): string {
  if (minutes < 60) return `${minutes} minutos`;
  const hours = minutes / 60;
  return hours === 1 ? '1 hora' : `${hours} horas`;
}

// Mensaje de error del servidor (texto plano o ProblemDetails) para mostrar en un aviso.
export function errorText(err: any, fallback: string): string {
  const data = err?.response?.data;
  if (typeof data === 'string' && data) return data;
  if (typeof data?.detail === 'string') return data.detail;
  if (typeof data?.title === 'string') return data.title;
  return fallback;
}

// Evento con el que el perfil avisa al menú del avatar que los datos cambiaron (detail: AccountMe).
export const ACCOUNT_CHANGED_EVENT = 'fel:account-changed';
