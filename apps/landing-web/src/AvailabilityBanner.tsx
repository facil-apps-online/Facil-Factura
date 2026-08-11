import { useEffect, useState } from 'react';
import { ShieldCheck, Globe, AlertCircle } from 'lucide-react';

type Availability = {
  status: 'checking' | 'available' | 'unavailable' | 'error';
  countryName?: string;
  availableIsoCodes: string[];
};

const API_URL = 'https://api.facil-factura.pro/api/tenant/countries';

async function detectCountryCode(): Promise<string | null> {
  try {
    const res = await fetch('https://ipwho.is/');
    const data = await res.json();
    return data?.country_code ?? null;
  } catch {
    return null;
  }
}

function useAvailability(): Availability {
  const [availability, setAvailability] = useState<Availability>({
    status: 'checking',
    availableIsoCodes: [],
  });

  useEffect(() => {
    let cancelled = false;

    (async () => {
      let availableIsoCodes: string[] = [];
      try {
        const res = await fetch(API_URL);
        const countries: { isoCode?: string; name?: string }[] = await res.json();
        availableIsoCodes = (countries ?? [])
          .map((c) => c.isoCode)
          .filter((c): c is string => Boolean(c))
          .map((c) => c.toUpperCase());
      } catch {
        // Si no se pueden cargar los países, no mostrar advertencia.
      }

      if (cancelled) return;

      if (availableIsoCodes.length === 0) {
        setAvailability({ status: 'error', availableIsoCodes: [] });
        return;
      }

      const visitorCode = await detectCountryCode();

      if (cancelled) return;

      if (!visitorCode) {
        setAvailability({ status: 'available', availableIsoCodes });
        return;
      }

      const isAvailable = availableIsoCodes.includes(visitorCode);

      if (isAvailable) {
        setAvailability({ status: 'available', availableIsoCodes });
      } else {
        const name = await nameForCode(visitorCode);
        if (cancelled) return;
        setAvailability({
          status: 'unavailable',
          countryName: name ?? visitorCode,
          availableIsoCodes,
        });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  return availability;
}

async function nameForCode(code: string): Promise<string | null> {
  const map: Record<string, string> = {
    CO: 'Colombia',
    CL: 'Chile',
    AR: 'Argentina',
    ES: 'España',
    MX: 'México',
    PE: 'Perú',
    EC: 'Ecuador',
    UY: 'Uruguay',
    BR: 'Brasil',
  };
  return map[code] ?? null;
}

export default function AvailabilityBanner() {
  const availability = useAvailability();

  if (availability.status === 'checking' || availability.status === 'error') {
    return null;
  }

  if (availability.status === 'available') {
    return (
      <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-emerald-100 text-emerald-700 text-xs font-semibold">
        <Globe size={14} />
        Disponible en tu país
      </div>
    );
  }

  return (
    <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-amber-100 text-amber-700 text-xs font-semibold">
      <AlertCircle size={14} />
      Disponible ahora solo en Colombia · Estamos ampliando cobertura en breve
    </div>
  );
}