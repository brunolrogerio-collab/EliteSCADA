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
    background: 'Fundo', image: 'imagem', color: 'cor', default: 'padrão', modeLabel: 'Usar', mode: { theme: 'Tema atual (claro/escuro)', color: 'Cor personalizada', image: 'Imagem de asset' }, colorLabel: 'Cor', clear: 'Limpar', imageAsset: 'Asset de imagem', chooseImage: 'Escolher imagem…', importingAsset: 'Importando…', noBackgroundImage: 'Sem imagem de fundo', imageFit: 'Ajuste da imagem', assetIdentityOnly: 'Somente identidade canônica do asset do projeto.', resetBackground: 'Restaurar fundo',
    fit: { cover: 'Cobrir', contain: 'Conter', stretch: 'Esticar', center: 'Centralizar', tile: 'Repetir' }
  },
  palette: {
    arc: 'Arco', bezier: 'Curva Bézier', alarmBrowser: 'Browser de Alarmes', eventBrowser: 'Browser de Eventos', videoPlayer: 'Vídeo', pdfViewer: 'Visualizador PDF'
  },
  dynamo: {
    name: 'Dínamo', definitionNotFound: 'Definição não encontrada no snapshot canônico de Engineering.', locked: 'Bloqueado', publicSuffix: 'públicos', instance: 'Instância', noPublicParameters: 'Nenhum parâmetro público.',
    statePreview: 'Preview do estado de Engineering', quality: 'Qualidade', settled: 'Estado', command: 'Comando', none: 'Nenhum', fault: 'Falha', alarm: 'Alarme', resolvedPriority: 'Prioridade resolvida', previewOnly: 'Somente preview. Nada é persistido.',
    good: 'Boa', uncertain: 'Incerta', bad: 'Ruim', stale: 'Desatualizada', unknown: 'Desconhecida', inactive: 'Inativo', active: 'Ativo', transitioning: 'Em transição', start: 'Partir', stop: 'Parar', open: 'Abrir', close: 'Fechar', increase: 'Aumentar', decrease: 'Diminuir', setpoint: 'Setpoint',
    trueValue: 'Verdadeiro', falseValue: 'Falso', requiredMissing: 'Valor obrigatório ausente', instanceValue: 'Valor da instância', defaultUnset: 'Padrão / não definido', reset: 'Restaurar', invalidValue: 'Valor inválido', selectTag: 'Selecione um TAG…', selectCommand: 'Selecione um Command…', notAssigned: 'Não atribuído', stateTagHint: 'TAG numérico de estado: 0 parado, 1 ligado, 2 falha.', valueSource: 'Fonte de valor', expression: 'Expressão', sourceResult: 'Tipo de resultado', dependencySource: 'Origem da dependência', expressionHint: 'Use “source” para referenciar a origem selecionada.', clientMemoryOutput: 'Saída de script (Client Memory)', clientMemoryOutputHint: 'Selecione a variável Client Memory escrita pelo script. Associe a execução do script na aba Eventos.', buttonAction: 'Ação do botão', buttonActionCommand: 'Executar comando autorizado', buttonActionAnalog: 'Escrever valor analógico', buttonActionSetBoolean: 'Definir bit (ligado/desligado)', buttonActionToggleBoolean: 'Alternar bit'
  },
  library: {
    title: 'Biblioteca de dínamos', hint: 'Busque componentes reutilizáveis de processo e insira instâncias configuradas.', search: 'Buscar', searchPlaceholder: 'Bomba, válvula, VFD…', category: 'Categoria', allCategories: 'Todas as categorias', results: 'Resultados de dínamos', noResults: 'Nenhum dínamo corresponde ao filtro.', preview: 'Preview do dínamo selecionado', noVisual: 'Sem geometria visual', publicInterface: 'Interface pública', noParameters: 'Sem parâmetros públicos', equipmentPath: 'Caminho do equipamento (opcional)', add: 'Adicionar dínamo', dimensions: 'Dimensões', version: 'Versão', source: 'Origem', builtIn: 'EliteSCADA integrada', required: 'obrigatório', categories: { pump: 'Bomba', motor: 'Motor', valve: 'Válvula', tank: 'Tanque', compressor: 'Compressor/Soprador', instrument: 'Instrumento', process: 'Equipamento de processo', electrical: 'Elétrica', substation: 'Subestação', other: 'Outros' }
  },
  runtimeState: { badQuality: 'QUALIDADE RUIM', fault: 'FALHA', alarm: 'ALARME', uncertain: 'INCERTO', inhibited: 'BLOQUEADO', command: 'COMANDO', transition: 'TRANSIÇÃO', active: 'ATIVO', inactive: 'INATIVO', unknown: 'DESCONHECIDO', feedbackMismatch: 'divergência de feedback', dynamoError: 'ERRO DE DÍNAMO' }
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
    background: 'Background', image: 'image', color: 'color', default: 'default', modeLabel: 'Use', mode: { theme: 'Current theme (light/dark)', color: 'Custom color', image: 'Image asset' }, colorLabel: 'Color', clear: 'Clear', imageAsset: 'Image asset', chooseImage: 'Choose image…', importingAsset: 'Importing…', noBackgroundImage: 'No background image', imageFit: 'Image fit', assetIdentityOnly: 'Canonical project asset identity only.', resetBackground: 'Reset background',
    fit: { cover: 'Cover', contain: 'Contain', stretch: 'Stretch', center: 'Center', tile: 'Tile' }
  },
  palette: {
    arc: 'Arc', bezier: 'Bezier curve', alarmBrowser: 'Alarm Browser', eventBrowser: 'Event Browser', videoPlayer: 'Video', pdfViewer: 'PDF viewer'
  },
  dynamo: {
    name: 'Dynamo', definitionNotFound: 'Definition not found in the canonical Engineering snapshot.', locked: 'Locked', publicSuffix: 'public', instance: 'Instance', noPublicParameters: 'No public parameters.',
    statePreview: 'Engineering state preview', quality: 'Quality', settled: 'Settled state', command: 'Command', none: 'None', fault: 'Fault', alarm: 'Alarm', resolvedPriority: 'Resolved priority', previewOnly: 'Preview only. Nothing is persisted.',
    good: 'Good', uncertain: 'Uncertain', bad: 'Bad', stale: 'Stale', unknown: 'Unknown', inactive: 'Inactive', active: 'Active', transitioning: 'Transitioning', start: 'Start', stop: 'Stop', open: 'Open', close: 'Close', increase: 'Increase', decrease: 'Decrease', setpoint: 'Setpoint',
    trueValue: 'True', falseValue: 'False', requiredMissing: 'Required value missing', instanceValue: 'Instance value', defaultUnset: 'Default / unset', reset: 'Reset', invalidValue: 'Invalid value', selectTag: 'Select TAG…', selectCommand: 'Select Command…', notAssigned: 'Not assigned', stateTagHint: 'Numeric state TAG: 0 stopped, 1 running, 2 fault.', valueSource: 'Value source', expression: 'Expression', sourceResult: 'Result type', dependencySource: 'Dependency source', expressionHint: 'Use “source” to reference the selected source.', clientMemoryOutput: 'Script output (Client Memory)', clientMemoryOutputHint: 'Select the Client Memory variable written by the script. Associate the script execution in the Events tab.', buttonAction: 'Button action', buttonActionCommand: 'Execute authorized command', buttonActionAnalog: 'Write analog value', buttonActionSetBoolean: 'Set bit (on/off)', buttonActionToggleBoolean: 'Toggle bit'
  },
  library: {
    title: 'Dynamo library', hint: 'Search reusable process components and place configured instances.', search: 'Search', searchPlaceholder: 'Pump, valve, VFD…', category: 'Category', allCategories: 'All categories', results: 'Dynamo results', noResults: 'No Dynamo matches this filter.', preview: 'Selected Dynamo preview', noVisual: 'No visual geometry', publicInterface: 'Public interface', noParameters: 'No public parameters', equipmentPath: 'Equipment path (optional)', add: 'Add Dynamo', dimensions: 'Dimensions', version: 'Version', source: 'Source', builtIn: 'Built into EliteSCADA', required: 'required', categories: { pump: 'Pump', motor: 'Motor', valve: 'Valve', tank: 'Tank', compressor: 'Compressor/Blower', instrument: 'Instrument', process: 'Process equipment', electrical: 'Electrical', substation: 'Substation', other: 'Other' }
  },
  runtimeState: { badQuality: 'BAD QUALITY', fault: 'FAULT', alarm: 'ALARM', uncertain: 'UNCERTAIN', inhibited: 'INHIBITED', command: 'COMMAND', transition: 'TRANSITION', active: 'ACTIVE', inactive: 'INACTIVE', unknown: 'UNKNOWN', feedbackMismatch: 'feedback mismatch', dynamoError: 'DYNAMO ERROR' }
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
    background: 'Fondo', image: 'imagen', color: 'color', default: 'predeterminado', modeLabel: 'Usar', mode: { theme: 'Tema actual (claro/oscuro)', color: 'Color personalizado', image: 'Recurso de imagen' }, colorLabel: 'Color', clear: 'Limpiar', imageAsset: 'Recurso de imagen', chooseImage: 'Elegir imagen…', importingAsset: 'Importando…', noBackgroundImage: 'Sin imagen de fondo', imageFit: 'Ajuste de imagen', assetIdentityOnly: 'Solo identidad canónica del recurso del proyecto.', resetBackground: 'Restablecer fondo',
    fit: { cover: 'Cubrir', contain: 'Contener', stretch: 'Estirar', center: 'Centrar', tile: 'Repetir' }
  },
  palette: {
    arc: 'Arco', bezier: 'Curva Bézier', alarmBrowser: 'Browser de Alarmas', eventBrowser: 'Browser de Eventos', videoPlayer: 'Vídeo', pdfViewer: 'Visor PDF'
  },
  dynamo: {
    name: 'Dínamo', definitionNotFound: 'Definición no encontrada en el snapshot canónico de Engineering.', locked: 'Bloqueado', publicSuffix: 'públicos', instance: 'Instancia', noPublicParameters: 'Sin parámetros públicos.',
    statePreview: 'Preview del estado de Engineering', quality: 'Calidad', settled: 'Estado', command: 'Comando', none: 'Ninguno', fault: 'Falla', alarm: 'Alarma', resolvedPriority: 'Prioridad resuelta', previewOnly: 'Solo preview. Nada se persiste.',
    good: 'Buena', uncertain: 'Incierta', bad: 'Mala', stale: 'Desactualizada', unknown: 'Desconocida', inactive: 'Inactivo', active: 'Activo', transitioning: 'En transición', start: 'Arrancar', stop: 'Parar', open: 'Abrir', close: 'Cerrar', increase: 'Aumentar', decrease: 'Disminuir', setpoint: 'Setpoint',
    trueValue: 'Verdadero', falseValue: 'Falso', requiredMissing: 'Falta un valor obligatorio', instanceValue: 'Valor de la instancia', defaultUnset: 'Predeterminado / no definido', reset: 'Restablecer', invalidValue: 'Valor inválido', selectTag: 'Seleccione un TAG…', selectCommand: 'Seleccione un Command…', notAssigned: 'No asignado', stateTagHint: 'TAG numérico de estado: 0 detenido, 1 en marcha, 2 falla.', valueSource: 'Fuente de valor', expression: 'Expresión', sourceResult: 'Tipo de resultado', dependencySource: 'Origen de dependencia', expressionHint: 'Use “source” para referirse al origen seleccionado.', clientMemoryOutput: 'Salida de script (Client Memory)', clientMemoryOutputHint: 'Seleccione la variable Client Memory escrita por el script. Asocie la ejecución del script en la pestaña Eventos.', buttonAction: 'Acción del botón', buttonActionCommand: 'Ejecutar comando autorizado', buttonActionAnalog: 'Escribir valor analógico', buttonActionSetBoolean: 'Definir bit (encendido/apagado)', buttonActionToggleBoolean: 'Alternar bit'
  },
  library: {
    title: 'Biblioteca de dínamos', hint: 'Busque componentes de proceso reutilizables y coloque instancias configuradas.', search: 'Buscar', searchPlaceholder: 'Bomba, válvula, VFD…', category: 'Categoría', allCategories: 'Todas las categorías', results: 'Resultados de dínamos', noResults: 'Ningún dínamo coincide con este filtro.', preview: 'Preview del dínamo seleccionado', noVisual: 'Sin geometría visual', publicInterface: 'Interfaz pública', noParameters: 'Sin parámetros públicos', equipmentPath: 'Ruta del equipo (opcional)', add: 'Agregar dínamo', dimensions: 'Dimensiones', version: 'Versión', source: 'Origen', builtIn: 'Integrado en EliteSCADA', required: 'obligatorio', categories: { pump: 'Bomba', motor: 'Motor', valve: 'Válvula', tank: 'Tanque', compressor: 'Compresor/Soplador', instrument: 'Instrumento', process: 'Equipos de proceso', electrical: 'Eléctrica', substation: 'Subestación', other: 'Otros' }
  },
  runtimeState: { badQuality: 'MALA CALIDAD', fault: 'FALLA', alarm: 'ALARMA', uncertain: 'INCIERTO', inhibited: 'INHIBIDO', command: 'COMANDO', transition: 'TRANSICIÓN', active: 'ACTIVO', inactive: 'INACTIVO', unknown: 'DESCONOCIDO', feedbackMismatch: 'divergencia de feedback', dynamoError: 'ERROR DE DÍNAMO' }
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
