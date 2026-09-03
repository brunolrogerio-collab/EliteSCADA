import React, { createContext, useContext } from 'react';
import type { EngineeringLocale } from '../i18n';

const ptBR = {
  toolbar: {
    aria: 'Operações de edição visual', history: 'Histórico', undo: 'Desfazer', redo: 'Refazer', copy: 'Copiar', paste: 'Colar',
    align: 'Alinhar', alignLeft: 'Alinhar à esquerda', alignHorizontalCenters: 'Alinhar centros horizontais', alignRight: 'Alinhar à direita', alignTop: 'Alinhar ao topo', alignVerticalMiddles: 'Alinhar centros verticais', alignBottom: 'Alinhar à base',
    distribute: 'Distribuir', distributeHorizontalCenters: 'Distribuir centros horizontalmente', distributeHorizontalSpacing: 'Distribuir espaçamento horizontal', distributeVerticalCenters: 'Distribuir centros verticalmente', distributeVerticalSpacing: 'Distribuir espaçamento vertical',
    size: 'Tamanho', sameWidth: 'Mesma largura', sameHeight: 'Mesma altura', sameSize: 'Mesmo tamanho',
    structure: 'Estrutura', group: 'Agrupar', ungroup: 'Desagrupar', lockSelection: 'Bloquear seleção', unlockSelection: 'Desbloquear seleção', lock: 'Bloquear', unlock: 'Desbloquear'
  },
  outliner: {
    title: 'Estrutura', hierarchy: 'Hierarquia de objetos visuais', expand: 'Expandir', collapse: 'Recolher', locked: 'Bloqueado', lockedByParent: 'Bloqueado pelo grupo pai'
  },
  surface: {
    background: 'Fundo', image: 'imagem', color: 'cor', default: 'padrão', colorLabel: 'Cor', clear: 'Limpar', imageAsset: 'Asset de imagem', noBackgroundImage: 'Sem imagem de fundo', imageFit: 'Ajuste da imagem', assetIdentityOnly: 'Somente identidade canônica do asset do projeto.', resetBackground: 'Restaurar fundo',
    fit: { cover: 'Cobrir', contain: 'Conter', stretch: 'Esticar', center: 'Centralizar', tile: 'Repetir' }
  },
  dynamo: {
    name: 'Dínamo', definitionNotFound: 'Definição não encontrada no snapshot canônico de Engineering.', locked: 'Bloqueado', publicSuffix: 'públicos', instance: 'Instância', noPublicParameters: 'Nenhum parâmetro público.',
    statePreview: 'Preview do estado de Engineering', quality: 'Qualidade', settled: 'Estado', command: 'Comando', none: 'Nenhum', fault: 'Falha', alarm: 'Alarme', resolvedPriority: 'Prioridade resolvida', previewOnly: 'Somente preview. Nada é persistido.',
    good: 'Boa', uncertain: 'Incerta', bad: 'Ruim', stale: 'Desatualizada', unknown: 'Desconhecida', inactive: 'Inativo', active: 'Ativo', transitioning: 'Em transição', start: 'Partir', stop: 'Parar', open: 'Abrir', close: 'Fechar', increase: 'Aumentar', decrease: 'Diminuir', setpoint: 'Setpoint',
    trueValue: 'Verdadeiro', falseValue: 'Falso', requiredMissing: 'Valor obrigatório ausente', instanceValue: 'Valor da instância', defaultUnset: 'Padrão / não definido', reset: 'Restaurar', invalidValue: 'Valor inválido', selectTag: 'Selecione um TAG…', notAssigned: 'Não atribuído'
  },
  library: {
    title: 'Biblioteca de dínamos', hint: 'Busque componentes reutilizáveis de processo e insira instâncias configuradas.', search: 'Buscar', searchPlaceholder: 'Bomba, válvula, VFD…', category: 'Categoria', allCategories: 'Todas as categorias', results: 'Resultados de dínamos', noResults: 'Nenhum dínamo corresponde ao filtro.', preview: 'Preview do dínamo selecionado', publicInterface: 'Interface pública', noParameters: 'Sem parâmetros públicos', equipmentPath: 'Caminho do equipamento (opcional)', add: 'Adicionar dínamo', categories: { pump: 'Bomba', motor: 'Motor', valve: 'Válvula', tank: 'Tanque', other: 'Outros' }
  },
  runtimeState: { badQuality: 'QUALIDADE RUIM', fault: 'FALHA', alarm: 'ALARME', uncertain: 'INCERTO', command: 'COMANDO', transition: 'TRANSIÇÃO', active: 'ATIVO', inactive: 'INATIVO', unknown: 'DESCONHECIDO', feedbackMismatch: 'divergência de feedback' }
} as const;

type DeepString<T> = { readonly [K in keyof T]: T[K] extends string ? string : DeepString<T[K]> };
type C07VisualEditorText = DeepString<typeof ptBR>;

const en: C07VisualEditorText = {
  toolbar: {
    aria: 'Visual authoring operations', history: 'History', undo: 'Undo', redo: 'Redo', copy: 'Copy', paste: 'Paste',
    align: 'Align', alignLeft: 'Align left', alignHorizontalCenters: 'Align horizontal centers', alignRight: 'Align right', alignTop: 'Align top', alignVerticalMiddles: 'Align vertical middles', alignBottom: 'Align bottom',
    distribute: 'Distribute', distributeHorizontalCenters: 'Distribute horizontal centers', distributeHorizontalSpacing: 'Distribute horizontal spacing', distributeVerticalCenters: 'Distribute vertical centers', distributeVerticalSpacing: 'Distribute vertical spacing',
    size: 'Size', sameWidth: 'Same width', sameHeight: 'Same height', sameSize: 'Same size',
    structure: 'Structure', group: 'Group', ungroup: 'Ungroup', lockSelection: 'Lock selection', unlockSelection: 'Unlock selection', lock: 'Lock', unlock: 'Unlock'
  },
  outliner: {
    title: 'Outliner', hierarchy: 'Visual object hierarchy', expand: 'Expand', collapse: 'Collapse', locked: 'Locked', lockedByParent: 'Locked by parent group'
  },
  surface: {
    background: 'Background', image: 'image', color: 'color', default: 'default', colorLabel: 'Color', clear: 'Clear', imageAsset: 'Image asset', noBackgroundImage: 'No background image', imageFit: 'Image fit', assetIdentityOnly: 'Canonical project asset identity only.', resetBackground: 'Reset background',
    fit: { cover: 'Cover', contain: 'Contain', stretch: 'Stretch', center: 'Center', tile: 'Tile' }
  },
  dynamo: {
    name: 'Dynamo', definitionNotFound: 'Definition not found in the canonical Engineering snapshot.', locked: 'Locked', publicSuffix: 'public', instance: 'Instance', noPublicParameters: 'No public parameters.',
    statePreview: 'Engineering state preview', quality: 'Quality', settled: 'Settled state', command: 'Command', none: 'None', fault: 'Fault', alarm: 'Alarm', resolvedPriority: 'Resolved priority', previewOnly: 'Preview only. Nothing is persisted.',
    good: 'Good', uncertain: 'Uncertain', bad: 'Bad', stale: 'Stale', unknown: 'Unknown', inactive: 'Inactive', active: 'Active', transitioning: 'Transitioning', start: 'Start', stop: 'Stop', open: 'Open', close: 'Close', increase: 'Increase', decrease: 'Decrease', setpoint: 'Setpoint',
    trueValue: 'True', falseValue: 'False', requiredMissing: 'Required value missing', instanceValue: 'Instance value', defaultUnset: 'Default / unset', reset: 'Reset', invalidValue: 'Invalid value', selectTag: 'Select TAG…', notAssigned: 'Not assigned'
  },
  library: {
    title: 'Dynamo library', hint: 'Search reusable process components and place configured instances.', search: 'Search', searchPlaceholder: 'Pump, valve, VFD…', category: 'Category', allCategories: 'All categories', results: 'Dynamo results', noResults: 'No Dynamo matches this filter.', preview: 'Selected Dynamo preview', publicInterface: 'Public interface', noParameters: 'No public parameters', equipmentPath: 'Equipment path (optional)', add: 'Add Dynamo', categories: { pump: 'Pump', motor: 'Motor', valve: 'Valve', tank: 'Tank', other: 'Other' }
  },
  runtimeState: { badQuality: 'BAD QUALITY', fault: 'FAULT', alarm: 'ALARM', uncertain: 'UNCERTAIN', command: 'COMMAND', transition: 'TRANSITION', active: 'ACTIVE', inactive: 'INACTIVE', unknown: 'UNKNOWN', feedbackMismatch: 'feedback mismatch' }
};

const es: C07VisualEditorText = {
  toolbar: {
    aria: 'Operaciones de edición visual', history: 'Historial', undo: 'Deshacer', redo: 'Rehacer', copy: 'Copiar', paste: 'Pegar',
    align: 'Alinear', alignLeft: 'Alinear a la izquierda', alignHorizontalCenters: 'Alinear centros horizontales', alignRight: 'Alinear a la derecha', alignTop: 'Alinear arriba', alignVerticalMiddles: 'Alinear centros verticales', alignBottom: 'Alinear abajo',
    distribute: 'Distribuir', distributeHorizontalCenters: 'Distribuir centros horizontalmente', distributeHorizontalSpacing: 'Distribuir espacio horizontal', distributeVerticalCenters: 'Distribuir centros verticalmente', distributeVerticalSpacing: 'Distribuir espacio vertical',
    size: 'Tamaño', sameWidth: 'Mismo ancho', sameHeight: 'Misma altura', sameSize: 'Mismo tamaño',
    structure: 'Estructura', group: 'Agrupar', ungroup: 'Desagrupar', lockSelection: 'Bloquear selección', unlockSelection: 'Desbloquear selección', lock: 'Bloquear', unlock: 'Desbloquear'
  },
  outliner: {
    title: 'Estructura', hierarchy: 'Jerarquía de objetos visuales', expand: 'Expandir', collapse: 'Contraer', locked: 'Bloqueado', lockedByParent: 'Bloqueado por el grupo padre'
  },
  surface: {
    background: 'Fondo', image: 'imagen', color: 'color', default: 'predeterminado', colorLabel: 'Color', clear: 'Limpiar', imageAsset: 'Recurso de imagen', noBackgroundImage: 'Sin imagen de fondo', imageFit: 'Ajuste de imagen', assetIdentityOnly: 'Solo identidad canónica del recurso del proyecto.', resetBackground: 'Restablecer fondo',
    fit: { cover: 'Cubrir', contain: 'Contener', stretch: 'Estirar', center: 'Centrar', tile: 'Repetir' }
  },
  dynamo: {
    name: 'Dínamo', definitionNotFound: 'Definición no encontrada en el snapshot canónico de Engineering.', locked: 'Bloqueado', publicSuffix: 'públicos', instance: 'Instancia', noPublicParameters: 'Sin parámetros públicos.',
    statePreview: 'Preview del estado de Engineering', quality: 'Calidad', settled: 'Estado', command: 'Comando', none: 'Ninguno', fault: 'Falla', alarm: 'Alarma', resolvedPriority: 'Prioridad resuelta', previewOnly: 'Solo preview. Nada se persiste.',
    good: 'Buena', uncertain: 'Incierta', bad: 'Mala', stale: 'Desactualizada', unknown: 'Desconocida', inactive: 'Inactivo', active: 'Activo', transitioning: 'En transición', start: 'Arrancar', stop: 'Parar', open: 'Abrir', close: 'Cerrar', increase: 'Aumentar', decrease: 'Disminuir', setpoint: 'Setpoint',
    trueValue: 'Verdadero', falseValue: 'Falso', requiredMissing: 'Falta un valor obligatorio', instanceValue: 'Valor de la instancia', defaultUnset: 'Predeterminado / no definido', reset: 'Restablecer', invalidValue: 'Valor inválido', selectTag: 'Seleccione un TAG…', notAssigned: 'No asignado'
  },
  library: {
    title: 'Biblioteca de dínamos', hint: 'Busque componentes de proceso reutilizables y coloque instancias configuradas.', search: 'Buscar', searchPlaceholder: 'Bomba, válvula, VFD…', category: 'Categoría', allCategories: 'Todas las categorías', results: 'Resultados de dínamos', noResults: 'Ningún dínamo coincide con este filtro.', preview: 'Preview del dínamo seleccionado', publicInterface: 'Interfaz pública', noParameters: 'Sin parámetros públicos', equipmentPath: 'Ruta del equipo (opcional)', add: 'Agregar dínamo', categories: { pump: 'Bomba', motor: 'Motor', valve: 'Válvula', tank: 'Tanque', other: 'Otros' }
  },
  runtimeState: { badQuality: 'MALA CALIDAD', fault: 'FALLA', alarm: 'ALARMA', uncertain: 'INCIERTO', command: 'COMANDO', transition: 'TRANSICIÓN', active: 'ACTIVO', inactive: 'INACTIVO', unknown: 'DESCONOCIDO', feedbackMismatch: 'divergencia de feedback' }
};

const resources: Record<EngineeringLocale, C07VisualEditorText> = { 'pt-BR': ptBR, en, es };
const C07VisualEditorTextContext = createContext<C07VisualEditorText>(ptBR);

export function c07VisualEditorText(locale: EngineeringLocale): C07VisualEditorText {
  return resources[locale];
}

export function C07VisualEditorI18nProvider({
  locale,
  children
}: Readonly<{ locale: EngineeringLocale; children: React.ReactNode }>) {
  return <C07VisualEditorTextContext.Provider value={resources[locale]}>{children}</C07VisualEditorTextContext.Provider>;
}

export function useC07VisualEditorText(): C07VisualEditorText {
  return useContext(C07VisualEditorTextContext);
}
