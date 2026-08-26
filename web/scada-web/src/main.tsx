import React, { useEffect, useMemo, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { AuthGate } from './auth/AuthGate';
import { EngineeringApp } from './engineering/EngineeringApp';
import './styles.css';

type TagMessage = {
  type: 'tagValueChanged';
  tag: { id: string; name: string; path: string; engineeringUnit?: string };
  value: unknown;
  quality: string;
  timestamp: string;
  source?: string;
};

type LiveTag = TagMessage['tag'] & { value: unknown; quality: string; timestamp: string };
type Alarm = {
  definitionId: string;
  name: string;
  tagId: string;
  type: string;
  priority: string;
  state: string;
  lastTransition: string;
  lastValue: unknown;
  area?: string;
  message?: string;
  acknowledgedBy?: string;
};

type HistorySample = { tagId: string; value: unknown; timestamp: string; quality: string; source?: string };

const API = (import.meta.env.VITE_SCADA_API ?? '').replace(/\/$/, '');
const WS = API
  ? API.replace(/^http/, 'ws') + '/ws/tags'
  : `${window.location.protocol === 'https:' ? 'wss:' : 'ws:'}//${window.location.host}/ws/tags`;

function n(v: unknown, digits = 1) {
  const numeric = Number(v);
  return Number.isFinite(numeric) ? numeric.toFixed(digits) : '--';
}

function RuntimeApp() {
  const [tags, setTags] = useState<Record<string, LiveTag>>({});
  const [connected, setConnected] = useState(false);
  const [modal, setModal] = useState(false);
  const [alarms, setAlarms] = useState<Alarm[]>([]);
  const [history, setHistory] = useState<HistorySample[]>([]);

  useEffect(() => {
    let ws: WebSocket | undefined;
    let retry: number | undefined;
    let stopped = false;

    const connect = () => {
      if (stopped) return;
      ws = new WebSocket(WS);
      ws.onopen = () => setConnected(true);
      ws.onclose = event => {
        setConnected(false);
        if (stopped) return;

        // The backend uses 1008 for an explicit identity/session revocation. In that case
        // a transient reconnect loop would keep presenting stale authenticated UI, so reload
        // once and let AuthGate revalidate /api/auth/me and return to login when appropriate.
        if (event.code === 1008) {
          stopped = true;
          window.location.reload();
          return;
        }

        retry = window.setTimeout(connect, 1500);
      };
      ws.onerror = () => ws?.close();
      ws.onmessage = event => {
        const msg = JSON.parse(event.data) as TagMessage;
        if (msg.type !== 'tagValueChanged') return;
        setTags(current => ({
          ...current,
          [msg.tag.path]: { ...msg.tag, value: msg.value, quality: msg.quality, timestamp: msg.timestamp }
        }));
      };
    };

    connect();
    return () => {
      stopped = true;
      if (retry) window.clearTimeout(retry);
      ws?.close();
    };
  }, []);

  useEffect(() => {
    let stopped = false;
    const load = async () => {
      try {
        const response = await fetch(`${API}/api/alarms?activeOnly=true`);
        if (response.ok && !stopped) setAlarms(await response.json());
      } catch { /* runtime keeps operating if alarm endpoint is temporarily unavailable */ }
    };
    void load();
    const timer = window.setInterval(load, 1500);
    return () => { stopped = true; window.clearInterval(timer); };
  }, []);

  const get = (path: string) => tags[path]?.value;
  const level = Number(get('Demo.Tank01.Level') ?? 0);
  const running = Boolean(get('Demo.P01.Running'));
  const fault = Boolean(get('Demo.P01.Fault'));
  const pumpClass = fault ? 'fault' : running ? 'running' : 'stopped';
  const tagCount = useMemo(() => Object.keys(tags).length, [tags]);

  const loadPumpHistory = async () => {
    const tag = tags['Demo.P01.Current'];
    if (!tag) return;
    const response = await fetch(`${API}/api/history/${tag.id}?limit=30`);
    if (response.ok) setHistory(await response.json());
  };

  const openPump = () => {
    setModal(true);
    void loadPumpHistory();
  };

  const acknowledge = async (alarm: Alarm) => {
    await fetch(`${API}/api/alarms/${alarm.definitionId}/ack`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ user: 'demo-operator' })
    });
  };

  return (
    <main className="shell">
      <header className="topbar">
        <div>
          <strong>SCADA Platform</strong>
          <span>Runtime 0.1-dev</span>
          <a className="runtime-engineering-link" href="/engineering">Engineering</a>
        </div>
        <div className={`connection ${connected ? 'online' : ''}`}>{connected ? 'ONLINE' : 'OFFLINE'} · {tagCount} TAGs</div>
      </header>

      {alarms.length > 0 && (
        <section className="alarm-banner">
          <strong>{alarms.length} alarme{alarms.length > 1 ? 's' : ''} ativo{alarms.length > 1 ? 's' : ''}</strong>
          <span>{alarms[0].message ?? alarms[0].name}</span>
        </section>
      )}

      <section className="process-card">
        <div className="process-title">Demo · Estação Elevatória</div>
        <div className="process">
          <div className="tank-wrap">
            <div className="tank">
              <div className="water" style={{ height: `${Math.max(0, Math.min(level, 100))}%` }} />
              <span>{n(level)}%</span>
            </div>
            <label>Reservatório TK01</label>
          </div>

          <div className="pipe horizontal first" />

          <button className={`pump ${pumpClass}`} onClick={openPump} title="Abrir detalhes da bomba">
            <svg viewBox="0 0 120 90" aria-label="Bomba P01">
              <circle cx="48" cy="45" r="30" />
              <path d="M70 29 L108 29 L108 61 L70 61 Z" />
              <circle cx="48" cy="45" r="9" />
            </svg>
            <strong>P01</strong><small>{fault ? 'FALHA' : running ? 'OPERANDO' : 'PARADA'}</small>
          </button>

          <div className="pipe horizontal second" />
          <div className="line-values">
            <div><span>Pressão</span><strong>{n(get('Demo.Discharge.Pressure'))} bar</strong></div>
            <div><span>Vazão</span><strong>{n(get('Demo.Discharge.Flow'))} m³/h</strong></div>
          </div>
        </div>
      </section>

      <section className="tag-strip">
        <Metric title="Corrente P01" value={`${n(get('Demo.P01.Current'))} A`} />
        <Metric title="Frequência P01" value={`${n(get('Demo.P01.Frequency'))} Hz`} />
        <Metric title="Qualidade" value={tags['Demo.P01.Current']?.quality ?? '--'} />
      </section>

      {alarms.length > 0 && (
        <section className="alarm-list">
          <div className="section-heading"><strong>Alarmes ativos</strong><span>Alarm Engine</span></div>
          {alarms.map(alarm => (
            <div className="alarm-row" key={alarm.definitionId}>
              <div><strong>{alarm.name}</strong><span>{alarm.message ?? alarm.type}</span></div>
              <div className="alarm-meta"><span>{alarm.priority}</span><span>{alarm.state}</span><button onClick={() => acknowledge(alarm)}>ACK</button></div>
            </div>
          ))}
        </section>
      )}

      {modal && (
        <div className="modal-backdrop" onMouseDown={() => setModal(false)}>
          <section className="modal" onMouseDown={e => e.stopPropagation()}>
            <header><div><strong>Bomba P01</strong><span>Demo.P01</span></div><button onClick={() => setModal(false)}>×</button></header>
            <div className="status-grid">
              <Metric title="Estado" value={fault ? 'FALHA' : running ? 'OPERANDO' : 'PARADA'} />
              <Metric title="Corrente" value={`${n(get('Demo.P01.Current'))} A`} />
              <Metric title="Frequência" value={`${n(get('Demo.P01.Frequency'))} Hz`} />
              <Metric title="Comunicação" value={connected ? 'GOOD' : 'BAD_COMM'} />
            </div>
            <div className="history-preview">
              <div className="section-heading"><strong>Histórico recente · Corrente</strong><span>{history.length} amostras</span></div>
              <div className="spark-values">{history.slice(-12).map((sample, i) => <span key={`${sample.timestamp}-${i}`}>{n(sample.value)}</span>)}</div>
            </div>
          </section>
        </div>
      )}
    </main>
  );
}

function Metric({ title, value }: { title: string; value: string }) {
  return <div className="metric"><span>{title}</span><strong>{value}</strong></div>;
}

const RootApp = window.location.pathname.startsWith('/engineering') ? EngineeringApp : RuntimeApp;
createRoot(document.getElementById('root')!).render(
  <AuthGate>
    <RootApp />
  </AuthGate>
);
