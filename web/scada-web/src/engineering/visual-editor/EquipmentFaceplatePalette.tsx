import React from 'react';
import type { EquipmentEngineering, TemplateEngineering } from '../types';
import type { EngineeringLocale } from '../i18n';
import type { VisualEditorMutationIntent } from './visualEditorContracts';
import { CanonicalVisualPreview } from './CanonicalVisualPreview';
import './DynamoLibraryPalette.css';

export function EquipmentFaceplatePalette({
  equipment, templates, locale, onMutationIntent
}: {
  equipment: readonly EquipmentEngineering[];
  templates: readonly TemplateEngineering[];
  locale: EngineeringLocale;
  onMutationIntent: (intent: VisualEditorMutationIntent) => void;
}) {
  const text = locale === 'en'
    ? { title: 'Equipment faceplates', hint: 'Place a linked instance. Its artwork always comes from the current Template.', empty: 'No linked equipment with a graphical Template.', add: 'Add linked instance', noVisual: 'Template has no artwork' }
    : locale === 'es'
      ? { title: 'Faceplates de equipo', hint: 'Inserte una instancia vinculada. Su gráfico siempre viene de la plantilla actual.', empty: 'No hay equipos vinculados a una plantilla gráfica.', add: 'Agregar instancia vinculada', noVisual: 'La plantilla no tiene gráfico' }
      : { title: 'Faceplates de equipamento', hint: 'Insira uma instância vinculada. O desenho sempre vem do Template atual.', empty: 'Nenhum equipamento vinculado a um Template gráfico.', add: 'Adicionar instância vinculada', noVisual: 'Template sem desenho' };
  const linked = equipment.flatMap(item => {
    const template = templates.find(candidate =>
      Boolean(item.templateId && candidate.id === item.templateId) ||
      Boolean(item.templateKey && candidate.key === item.templateKey)
    );
    return template?.elements?.length ? [{ equipment: item, template }] : [];
  });
  return <section className="visual-dynamo-library equipment-faceplate-palette" data-testid="equipment-faceplate-palette">
    <header><strong>{text.title}</strong><span>{text.hint}</span></header>
    {linked.length ? <div className="visual-dynamo-library__grid" role="list">
      {linked.map(({ equipment: item, template }) => <article className="visual-dynamo-library__card equipment-faceplate-palette__card" key={item.id ?? item.path}>
        <CanonicalVisualPreview elements={template.elements ?? []} locale={locale} width={70} height={60} emptyLabel={text.noVisual} variant="thumbnail"/>
        <span className="visual-dynamo-library__card-copy"><strong>{item.name}</strong><code>{item.path}</code><small>{template.name}</small></span>
        <button className="visual-dynamo-library__add" type="button" disabled={!item.id} onClick={() => {
          const size = templateSize(template.elements ?? []);
          onMutationIntent({
          kind: 'equipment.add', equipmentId: item.id!, equipmentPath: item.path, equipmentName: item.name,
          defaultWidth: size.width, defaultHeight: size.height
        });
        }}>{text.add}</button>
      </article>)}
    </div> : <p className="visual-dynamo-library__empty">{text.empty}</p>}
  </section>;
}

function templateSize(elements: readonly import('../types').VisualElementEngineering[]) {
  let width = 260;
  let height = 180;
  const visit = (items: readonly import('../types').VisualElementEngineering[]) => {
    for (const element of items) {
      const properties = element.properties ?? {};
      const x = finite(properties.x), y = finite(properties.y);
      const objectWidth = finite(properties.width), objectHeight = finite(properties.height);
      width = Math.max(width, x + objectWidth);
      height = Math.max(height, y + objectHeight);
      if (element.children) visit(element.children);
    }
  };
  visit(elements);
  return { width: Math.min(width, 1600), height: Math.min(height, 1000) };
}

function finite(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : 0;
}
