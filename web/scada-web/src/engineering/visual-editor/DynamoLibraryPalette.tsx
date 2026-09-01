import React, { useMemo, useState } from 'react';
import type { DynamoEngineering } from '../types';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import './DynamoLibraryPalette.css';

export function DynamoLibraryPalette({
  definitions,
  onMutationIntent,
  locale
}: {
  definitions: readonly DynamoEngineering[];
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
  locale: 'pt-BR' | 'en' | 'es';
}) {
  const text = copy(locale);
  const sorted = useMemo(
    () => [...definitions].sort((left, right) => left.name.localeCompare(right.name, locale)),
    [definitions, locale]
  );
  const [selectedKey, setSelectedKey] = useState(sorted[0]?.key ?? '');
  const [equipmentPath, setEquipmentPath] = useState('');
  const selected = sorted.find(definition => definition.key === selectedKey) ?? sorted[0];

  if (!selected) return null;
  const width = positiveDimension(selected.properties?.defaultWidth, 120);
  const height = positiveDimension(selected.properties?.defaultHeight, 100);

  return <section className="visual-dynamo-library" data-testid="visual-dynamo-library">
    <header><strong>{text.title}</strong><span>{text.hint}</span></header>
    <label>
      <span>{text.symbol}</span>
      <select value={selected.key} onChange={event => setSelectedKey(event.currentTarget.value)}>
        {sorted.map(definition => <option key={definition.key} value={definition.key}>
          {definition.name} · {definition.key}
        </option>)}
      </select>
    </label>
    <label>
      <span>{text.equipmentPath}</span>
      <input value={equipmentPath} placeholder="Plant.P01" onChange={event => setEquipmentPath(event.currentTarget.value)} />
    </label>
    <button type="button" onClick={() => onMutationIntent({
      kind: 'dynamo.add',
      dynamoKey: selected.key,
      equipmentPath: equipmentPath.trim() || null,
      defaultWidth: width,
      defaultHeight: height
    })}>{text.add}</button>
  </section>;
}

function positiveDimension(value: string | undefined, fallback: number): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

function copy(locale: 'pt-BR' | 'en' | 'es') {
  if (locale === 'en') return { title: 'Dynamo library', hint: 'Reusable process symbols included in the project.', symbol: 'Dynamo', equipmentPath: 'Equipment path (optional)', add: 'Add Dynamo' };
  if (locale === 'es') return { title: 'Biblioteca de dínamos', hint: 'Símbolos de proceso reutilizables incluidos en el proyecto.', symbol: 'Dínamo', equipmentPath: 'Ruta del equipo (opcional)', add: 'Agregar dínamo' };
  return { title: 'Biblioteca de dínamos', hint: 'Símbolos reutilizáveis de processo incluídos no projeto.', symbol: 'Dínamo', equipmentPath: 'Caminho do equipamento (opcional)', add: 'Adicionar dínamo' };
}
