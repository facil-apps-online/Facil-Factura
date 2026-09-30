// Mide la fortaleza de una contraseña y expone si cumple la política mínima.
//
// La regla de "cumple o no" espeja EXACTAMENTE a Fel.Core.Security.PasswordPolicy (8+ caracteres,
// al menos 3 de 4 grupos: mayúsculas, minúsculas, números, símbolos) — es el backend quien
// finalmente decide, esto solo evita que alguien envíe una contraseña que ya sabemos que va a
// rechazar. El puntaje de la barra es un indicador de "qué tan por encima del mínimo" queda, no
// reemplaza la regla dura.
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MIN_CLASSES = 3;

export interface PasswordStrengthResult {
  meetsPolicy: boolean;
  reason: string | null;
  score: number; // 0 a 4
  label: string;
}

function countCharacterClasses(password: string): number {
  const classes = [
    /[a-z]/.test(password),
    /[A-Z]/.test(password),
    /[0-9]/.test(password),
    /[^a-zA-Z0-9]/.test(password),
  ];
  return classes.filter(Boolean).length;
}

export function evaluatePassword(password: string): PasswordStrengthResult {
  if (!password) {
    return { meetsPolicy: false, reason: 'La contraseña es obligatoria.', score: 0, label: 'Vacía' };
  }

  const classes = countCharacterClasses(password);
  const meetsPolicy = password.length >= PASSWORD_MIN_LENGTH && classes >= PASSWORD_MIN_CLASSES;

  let reason: string | null = null;
  if (password.length < PASSWORD_MIN_LENGTH) {
    reason = `Debe tener al menos ${PASSWORD_MIN_LENGTH} caracteres.`;
  } else if (classes < PASSWORD_MIN_CLASSES) {
    reason = `Combina al menos ${PASSWORD_MIN_CLASSES} de estos: mayúsculas, minúsculas, números y símbolos.`;
  }

  // Puntaje 0-4, solo para la barra: longitud (0-2) + variedad más allá del mínimo exigido (0-2).
  let lengthScore = 0;
  if (password.length >= PASSWORD_MIN_LENGTH) lengthScore = 1;
  if (password.length >= 12) lengthScore = 2;

  const varietyScore = Math.max(0, Math.min(2, classes - 2));
  const score = Math.min(4, lengthScore + varietyScore);

  const labels = ['Muy débil', 'Débil', 'Media', 'Fuerte', 'Muy fuerte'];

  return { meetsPolicy, reason, score, label: labels[score] };
}

const BAR_COLORS = ['bg-rose-500', 'bg-orange-500', 'bg-amber-500', 'bg-lime-500', 'bg-emerald-500'];
const LABEL_COLORS = ['text-rose-600', 'text-orange-600', 'text-amber-600', 'text-lime-700', 'text-emerald-700'];

export default function PasswordStrength({ password }: { password: string }) {
  const result = evaluatePassword(password);

  if (!password) return null;

  return (
    <div className="mt-1.5 space-y-1">
      <div className="flex gap-1">
        {[0, 1, 2, 3].map(i => (
          <div
            key={i}
            className={`h-1.5 flex-1 rounded-full transition-colors ${i <= result.score ? BAR_COLORS[result.score] : 'bg-slate-200'}`}
          />
        ))}
      </div>
      <p className={`text-xs font-medium ${LABEL_COLORS[result.score]}`}>
        {result.label}
        {result.reason && <span className="text-slate-400 font-normal"> — {result.reason}</span>}
      </p>
    </div>
  );
}
