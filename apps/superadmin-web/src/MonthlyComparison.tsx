import { useEffect, useState } from 'react';
import { Loader2, TrendingDown, TrendingUp } from 'lucide-react';
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { api } from './api';

interface ComparisonPoint {
  day: number;
  current: number | null;
  projected: number | null;
  previous: number | null;
}

interface MonthlyComparisonData {
  day: number;
  daysInMonth: number;
  generatedToDate: number;
  projectedLow: number;
  projectedHigh: number;
  projectedCentral: number;
  previousSameDay: number;
  previousMonthTotal: number;
  vsPreviousSameDayPercent: number | null;
  projectedVsPreviousTotalPercent: number | null;
  currentLabel: string;
  previousLabel: string;
  series: ComparisonPoint[];
}

const money = (value: number) => `$${Math.round(value).toLocaleString('es-CO')}`;

// Millones abreviados en el eje para que no ocupe media pantalla.
const axisMoney = (value: number) => (value >= 1_000_000 ? `$${(value / 1_000_000).toLocaleString('es-CO', { maximumFractionDigits: 1 })} M` : `$${Math.round(value / 1000)} mil`);

const Variation = ({ percent, reference }: { percent: number | null; reference: string }) => {
  if (percent === null) return <p className="text-slate-500 text-xs mt-2">Sin dato del mes anterior para comparar</p>;
  const up = percent >= 0;
  const Icon = up ? TrendingUp : TrendingDown;
  return (
    <p className={`text-xs mt-2 flex items-center gap-1 ${up ? 'text-emerald-400' : 'text-rose-400'}`}>
      <Icon className="w-3.5 h-3.5" />
      {up ? '+' : ''}{percent.toLocaleString('es-CO')}% {reference}
    </p>
  );
};

export const MonthlyComparison = () => {
  const [data, setData] = useState<MonthlyComparisonData | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    api.get<MonthlyComparisonData>('/dashboard/monthly-comparison')
      .then(res => setData(res.data))
      .catch(() => setFailed(true));
  }, []);

  if (failed) return <p className="text-rose-400 text-sm mt-8">No se pudo cargar la comparación del mes.</p>;
  if (!data) return <div className="flex justify-center mt-12"><Loader2 className="w-6 h-6 animate-spin text-slate-500" /></div>;

  const elapsedPercent = Math.round((data.day / data.daysInMonth) * 100);

  return (
    <div className="mt-8 space-y-6">
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="glass-panel p-6 rounded-2xl">
          <p className="text-slate-400 text-sm font-semibold uppercase tracking-wider">Generado a hoy</p>
          <p className="text-4xl font-extrabold text-white mt-4">{money(data.generatedToDate)}</p>
          <Variation percent={data.vsPreviousSameDayPercent} reference={`frente al día ${data.day} de ${data.previousLabel}`} />
        </div>
        <div className="glass-panel p-6 rounded-2xl">
          <p className="text-slate-400 text-sm font-semibold uppercase tracking-wider">Proyección a fin de mes</p>
          <p className="text-4xl font-extrabold text-indigo-400 mt-4">{money(data.projectedCentral)}</p>
          <p className="text-slate-500 text-xs mt-2">Entre {money(data.projectedLow)} y {money(data.projectedHigh)}</p>
          <Variation percent={data.projectedVsPreviousTotalPercent} reference={`frente al cierre de ${data.previousLabel}`} />
        </div>
        <div className="glass-panel p-6 rounded-2xl">
          <p className="text-slate-400 text-sm font-semibold uppercase tracking-wider">Cierre del mes anterior</p>
          <p className="text-4xl font-extrabold text-slate-300 mt-4">{money(data.previousMonthTotal)}</p>
          <p className="text-slate-500 text-xs mt-2">Va el {elapsedPercent}% de {data.currentLabel} (día {data.day} de {data.daysInMonth})</p>
        </div>
      </div>

      <div className="glass-panel p-6 rounded-2xl">
        <h2 className="text-lg font-bold text-white">Acumulado por día: {data.currentLabel} contra {data.previousLabel}</h2>
        <p className="text-slate-500 text-xs mt-1">La línea punteada es la proyección del mes en curso. Los valores son a tarifa estándar, antes de descuentos de bolsas prepago.</p>
        <div className="h-80 mt-6">
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={data.series} margin={{ top: 8, right: 16, bottom: 0, left: 8 }}>
              <CartesianGrid stroke="#334155" strokeDasharray="3 3" />
              <XAxis dataKey="day" stroke="#94a3b8" tick={{ fontSize: 12 }} />
              <YAxis stroke="#94a3b8" tick={{ fontSize: 12 }} tickFormatter={axisMoney} width={72} />
              <Tooltip
                contentStyle={{ background: '#0f172a', border: '1px solid #334155', borderRadius: 12 }}
                labelStyle={{ color: '#e2e8f0' }}
                labelFormatter={day => `Día ${day}`}
                formatter={(value, name) => [money(Number(value)), name]}
              />
              <Legend />
              <Line type="monotone" dataKey="previous" name={data.previousLabel} stroke="#94a3b8" strokeWidth={2} dot={false} connectNulls={false} />
              <Line type="monotone" dataKey="current" name={data.currentLabel} stroke="#818cf8" strokeWidth={3} dot={false} connectNulls={false} />
              <Line type="monotone" dataKey="projected" name="Proyección" stroke="#818cf8" strokeWidth={2} strokeDasharray="6 6" dot={false} connectNulls={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>
    </div>
  );
};
