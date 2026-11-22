import { useEffect, useState } from 'react';

export function useMobileRuntime() {
  const [mobile, setMobile] = useState(() => window.matchMedia('(pointer: coarse)').matches);
  useEffect(() => {
    const query = window.matchMedia('(pointer: coarse)');
    const changed = () => setMobile(query.matches);
    query.addEventListener('change', changed);
    return () => query.removeEventListener('change', changed);
  }, []);
  return mobile;
}
