import React, { useMemo, useState } from 'react';
import { applyEngineeringPackage, loadEngineeringWorkspace, previewEngineeringPackage } from './api';
import type { EngineeringLocale } from './i18n';
import type { EngineeringSnapshot, EquipmentEngineering } from './types';
import { backendReferenceFromName } from './backendReferenceFromName';
import './object-catalog.css';

type Props = { snapshot: EngineeringSnapshot; locale: EngineeringLocale; onApplied: () => Promise<void> };

export function EquipmentFaceplateWorkspace({ snapshot, locale, onApplied }: Props) {
  const text = useMemo(() => copy(locale), [locale]);
  const [name, setName] = useState('');
  const [templateId, setTemplateId] = useState('');
  const [selectedId, setSelectedId] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const templates = snapshot.package.templates ?? [];
  const equipment = snapshot.package.equipment ?? [];
  const selected = equipment.find(item => item.id === selectedId || item.path === selectedId) ?? null;
  const selectedTemplate = selected && templates.find(item =>
    Boolean(selected.templateId && item.id === selected.templateId) ||
    Boolean(selected.templateKey && item.key === selected.templateKey)
  );

  const edit = (item: EquipmentEngineering) => {
    setSelectedId(item.id ?? '');
    setName(item.name);
    const template = templates.find(candidate =>
      Boolean(item.templateId && candidate.id === item.templateId) ||
      Boolean(item.templateKey && candidate.key === item.templateKey)
    );
    setTemplateId(template?.id ?? '');
    setMessage('');
  };

  const reset = () => {
    setSelectedId('');
    setName('');
    setTemplateId('');
    setMessage('');
  };

  const save = async () => {
    const cleanName = name.trim();
    const template = templateId ? templates.find(item => item.id === templateId) : undefined;
    if (!cleanName) {
      setMessage(text.nameRequired);
      return;
    }
    if (templateId && !template) {
      setMessage(text.templateUnavailable);
      return;
    }
    const path = selected?.path ?? `equipment.${backendReferenceFromName(cleanName)}`;
    if (!selected && equipment.some(item => item.path.toLocaleLowerCase() === path.toLocaleLowerCase())) {
      setMessage(text.duplicatePath);
      return;
    }
    const next: EquipmentEngineering = {
      ...(selected?.id ? { id: selected.id } : {}),
      path,
      name: cleanName,
      templateId: template?.id ?? null,
      templateKey: template?.key ?? null,
      bindings: selected?.bindings ?? []
    };
    const candidate = structuredClone(snapshot.package);
    candidate.equipment = selected
      ? equipment.map(item => (selected.id ? item.id === selected.id : item.path === selected.path) ? next : item)
      : [...equipment, next];

    setBusy(true);
    setMessage('');
    try {
      const before = await loadEngineeringWorkspace();
      const preview = await previewEngineeringPackage(candidate);
      const after = await loadEngineeringWorkspace();
      if (before.changeVersion !== after.changeVersion || after.changeVersion !== snapshot.workspace.changeVersion) {
        throw new Error(text.changedWorkspace);
      }
      if (!preview.canApply) {
        const firstIssue = preview.items.flatMap(item => item.issues ?? []).find(issue => issue.isError);
        throw new Error(firstIssue?.message ?? text.validationFailed);
      }
      await applyEngineeringPackage(candidate, after.changeVersion);
      await onApplied();
      setSelectedId(next.id ?? next.path);
      setMessage(text.saved);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : String(error));
    } finally {
      setBusy(false);
    }
  };

  return <section className="eng-section equipment-faceplate" data-testid="equipment-faceplate-workspace">
    <header className="eng-section-header"><div><span className="eng-eyebrow">{text.eyebrow}</span><h1>{text.title}</h1><p>{text.hint}</p></div><div className="eng-section-meta"><strong>{equipment.length} {text.items}</strong></div></header>
    <div className="equipment-faceplate__layout">
      <section className="eng-panel equipment-faceplate__editor">
        <h2>{selected ? text.edit : text.create}</h2>
        <label><span>{text.name}</span><input value={name} onChange={event => setName(event.currentTarget.value)} placeholder={text.namePlaceholder}/></label>
        <small>{selected ? `${text.path}: ${selected.path}` : `${text.pathGenerated} ${name.trim() ? `equipment.${backendReferenceFromName(name)}` : '—'}`}</small>
        <label><span>{text.template}</span><select value={templateId} onChange={event => setTemplateId(event.currentTarget.value)}>
          <option value="">{text.noTemplate}</option>
          {templates.map(template => <option key={template.id ?? template.key} value={template.id ?? ''}>{template.name} · {template.key}</option>)}
        </select></label>
        {selectedTemplate ? <div className="equipment-faceplate__link" data-testid="equipment-template-link"><strong>{text.linked}</strong><span>{selectedTemplate.name}</span><code>{selectedTemplate.key}</code><small>{text.linkBehavior}</small></div> : null}
        {templates.length === 0 ? <div className="eng-empty"><strong>{text.noTemplates}</strong><span>{text.templateOptionalHint}</span></div> : null}
        <div className="equipment-faceplate__actions"><button className="primary" type="button" disabled={busy} onClick={() => void save()}>{busy ? text.saving : selected ? text.save : text.create}</button>{selected ? <button className="secondary" type="button" disabled={busy} onClick={reset}>{text.newEquipment}</button> : null}</div>
        {message ? <p className="equipment-faceplate__message" role="status">{message}</p> : null}
      </section>
      <section className="eng-panel equipment-faceplate__list"><h2>{text.instances}</h2>
        {equipment.length === 0 ? <div className="eng-empty"><strong>{text.noEquipment}</strong><span>{text.emptyHint}</span></div> : equipment.map(item => {
          const template = templates.find(candidate =>
            Boolean(item.templateId && candidate.id === item.templateId) ||
            Boolean(item.templateKey && candidate.key === item.templateKey)
          );
          return <button type="button" className={`equipment-faceplate__item${selectedId === (item.id ?? item.path) ? ' is-selected' : ''}`} key={item.id ?? item.path} onClick={() => edit(item)}>
            <strong>{item.name}</strong><code>{item.path}</code><span>{template?.name ?? text.templateUnavailable}</span>
          </button>;
        })}
      </section>
    </div>
  </section>;
}

function copy(locale: EngineeringLocale) {
  if (locale === 'en') return {
    eyebrow: 'Equipment', title: 'Equipment instances', hint: 'Create equipment independently. A graphical Template/Faceplate is optional and can be attached, changed or detached later without changing the equipment identity.', items: 'instances', edit: 'Edit instance', create: 'Create equipment', name: 'Equipment name', namePlaceholder: 'e.g. Pump 01', path: 'Stable path', pathGenerated: 'Generated path:', template: 'Faceplate Template (optional)', noTemplate: 'No Template', linked: 'Linked to current Template', linkBehavior: 'Edits to the Template are reflected by every linked instance.', noTemplates: 'No Templates yet', templateOptionalHint: 'You can create the equipment now and attach a Template later.', instances: 'Equipment list', noEquipment: 'No equipment instances', emptyHint: 'Create one here and select the Template it should follow.', templateUnavailable: 'Template unavailable', nameRequired: 'Enter an equipment name.', duplicatePath: 'Another equipment instance already uses this generated path.', changedWorkspace: 'Engineering changed during validation. Reload and try again.', validationFailed: 'The candidate did not pass Engineering validation.', saved: 'Equipment link saved.', saving: 'Validating…', save: 'Save changes', newEquipment: 'New equipment'
  };
  if (locale === 'es') return {
    eyebrow: 'Equipos', title: 'Instancias de Equipo', hint: 'Cree equipos de forma independiente. La Plantilla/Faceplate gráfica es opcional y puede asociarse, cambiarse o quitarse después sin cambiar la identidad del equipo.', items: 'instancias', edit: 'Editar instancia', create: 'Crear equipo', name: 'Nombre del equipo', namePlaceholder: 'p. ej. Bomba 01', path: 'Ruta estable', pathGenerated: 'Ruta generada:', template: 'Plantilla Faceplate (opcional)', noTemplate: 'Sin plantilla', linked: 'Vinculado a la plantilla actual', linkBehavior: 'Los cambios en la plantilla se reflejan en todas las instancias vinculadas.', noTemplates: 'Aún no hay plantillas', templateOptionalHint: 'Puede crear el equipo ahora y asociar una plantilla después.', instances: 'Lista de equipos', noEquipment: 'Sin instancias de equipo', emptyHint: 'Cree una y seleccione la plantilla que debe seguir.', templateUnavailable: 'Plantilla no disponible', nameRequired: 'Ingrese el nombre del equipo.', duplicatePath: 'Otra instancia ya utiliza esta ruta generada.', changedWorkspace: 'Engineering cambió durante la validación. Recargue e intente de nuevo.', validationFailed: 'El candidato no pasó la validación de Engineering.', saved: 'Vínculo del equipo guardado.', saving: 'Validando…', save: 'Guardar cambios', newEquipment: 'Nuevo equipo'
  };
  return {
    eyebrow: 'Equipamentos', title: 'Instâncias de Equipamentos', hint: 'Crie equipamentos de forma independente. O Template/Faceplate gráfico é opcional e pode ser associado, trocado ou removido depois sem mudar a identidade do equipamento.', items: 'instâncias', edit: 'Editar instância', create: 'Criar equipamento', name: 'Nome do equipamento', namePlaceholder: 'ex.: Bomba 01', path: 'Caminho estável', pathGenerated: 'Caminho gerado:', template: 'Template Faceplate (opcional)', noTemplate: 'Sem Template', linked: 'Vinculado ao Template atual', linkBehavior: 'Alterações no Template são refletidas em todas as instâncias vinculadas.', noTemplates: 'Ainda não há Templates', templateOptionalHint: 'Você pode criar o equipamento agora e associar um Template depois.', instances: 'Lista de equipamentos', noEquipment: 'Nenhuma instância de equipamento', emptyHint: 'Crie uma e selecione o Template que ela deve acompanhar.', templateUnavailable: 'Template indisponível', nameRequired: 'Informe o nome do equipamento.', duplicatePath: 'Outra instância já usa esse caminho gerado.', changedWorkspace: 'A Engenharia mudou durante a validação. Recarregue e tente novamente.', validationFailed: 'O candidato não passou na validação da Engenharia.', saved: 'Vínculo do equipamento salvo.', saving: 'Validando…', save: 'Salvar alterações', newEquipment: 'Novo equipamento'
  };
}
