import { applyHistoricalPreset, localTimeZoneLabel, validateHistoricalTimeRange } from '../historicalTimeRange';
import type { EngineeringLocale } from '../../engineering/i18n';
import { useHistoricalPlayback } from './HistoricalPlaybackContext';
import { historicalPlaybackCopy } from './historicalPlaybackI18n';

const QUICK = [15 * 60, 60 * 60, 8 * 60 * 60, 24 * 60 * 60] as const;
const LABEL: Record<number,string> = {900:'15 min',3600:'1 h',28800:'8 h',86400:'24 h'};

export function HistoricalPlaybackOverlay({ locale, onClose }: { locale: EngineeringLocale; onClose: () => void }) {
  const playback = useHistoricalPlayback();
  const text = historicalPlaybackCopy(locale);
  const valid = validateHistoricalTimeRange(playback.timeRange).ok;
  const localInstant = playback.atUtc ? new Intl.DateTimeFormat(locale,{dateStyle:'short',timeStyle:'medium'}).format(new Date(playback.atUtc)) : '—';
  const custom = playback.timeRange.mode === 'absolute';

  const chooseQuick = (seconds:number) => playback.setTimeRange(Object.freeze({
    ...applyHistoricalPreset(playback.timeRange, seconds),
    mode:'relative' as const
  }));
  const chooseCustom = () => playback.setTimeRange(Object.freeze({ ...playback.timeRange, mode:'absolute' as const }));

  return <aside id="runtime-playback-overlay" className="runtime-operator-overlay runtime-playback-overlay"
    aria-label={text.title} data-testid="runtime-playback-overlay">
    <div className="runtime-operator-overlay-header">
      <strong>{text.title}</strong>
      <button type="button" className="runtime-operator-button" onClick={onClose}>{text.close}</button>
    </div>
    <div className="runtime-operator-overlay-content runtime-playback-overlay-content" data-runtime-session-control
      data-playback-mode={playback.mode} data-playback-load-state={playback.loadState}
      data-playback-gap-count={playback.gapCount} data-playback-position={playback.position}>
      {playback.mode === 'live' ? <p className="runtime-playback-hint">{text.liveHint}</p> : null}
      <div className="runtime-playback-quick" role="group" aria-label={text.title}>
        {QUICK.map(seconds => <button type="button" key={seconds}
          className={playback.timeRange.mode === 'relative' &&
            playback.timeRange.relativeAmount * (playback.timeRange.relativeUnit === 'hours' ? 3600 : playback.timeRange.relativeUnit === 'minutes' ? 60 : playback.timeRange.relativeUnit === 'days' ? 86400 : 1) === seconds ? 'active' : ''}
          onClick={() => chooseQuick(seconds)}>{LABEL[seconds]}</button>)}
        <button type="button" className={custom ? 'active' : ''} onClick={chooseCustom}>{text.custom}</button>
      </div>
      {custom ? <div className="runtime-playback-custom">
        <label><span>{text.from}</span><input type="datetime-local" step={1} value={playback.timeRange.absoluteFromLocal}
          onChange={e => playback.setTimeRange(Object.freeze({...playback.timeRange,absoluteFromLocal:e.target.value}))}/></label>
        <label><span>{text.to}</span><input type="datetime-local" step={1} value={playback.timeRange.absoluteToLocal}
          onChange={e => playback.setTimeRange(Object.freeze({...playback.timeRange,absoluteToLocal:e.target.value}))}/></label>
      </div> : null}
      <div className="runtime-playback-instant">
        <label><span>{text.instant}</span>
          <input type="range" min={0} max={1000} step={1} value={Math.round(playback.position*1000)}
            onChange={e => playback.setPosition(Number(e.target.value)/1000)}/></label>
        <output>{localInstant} · {localTimeZoneLabel()}</output>
      </div>
      <div className="runtime-playback-status">
        {playback.loadState === 'loading' ? <span>{text.loading}</span> : null}
        {playback.loadState === 'ready' ? <span>{playback.resolvedTagCount} {text.tags} · {playback.gapCount} {text.gaps}</span> : null}
        {playback.error ? <span role="alert">{playback.error}</span> : null}
      </div>
      <div className="runtime-playback-actions">
        {playback.mode === 'live'
          ? <button type="button" className="runtime-operator-button" disabled={!valid || !playback.visualScope} onClick={playback.enterPlayback}>{text.enter}</button>
          : <button type="button" className="runtime-operator-button runtime-playback-exit" onClick={playback.exitPlayback}>{text.backLive}</button>}
        {playback.mode === 'historicalPlayback' ? <button type="button" className="runtime-operator-button" onClick={playback.refresh}>↻</button> : null}
      </div>
    </div>
  </aside>;
}
