"""Keep long-form system chapters aligned with installed localized Help content."""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'src/Scada.Api/Runtime/HelpGuides/system-guides.json'
REVIEW_MARKERS = json.loads((ROOT / 'docs/manual/PRODUCT-CHANGE-REVIEW-MARKERS.json').read_text(encoding='utf-8'))
CHAPTERS = {
    'pt-BR': [('18-SEGURANCA-E-AUDITORIA.md', 'Segurança e auditoria: contas, permissões e proteção', ['security.local-accounts', 'security.policy-scopes', 'audit.query-diagnostics', 'security.lock-workflow']), ('17-ALARMES-E-EVENTOS.md', 'Alarmes e eventos: configuração e operação', ['alarms.configuration', 'alarms.runtime-operation', 'operational-events.configuration']), ('07-HISTORIAN-CAPTURA-CONSULTA.md', 'Historian: captura e consulta', ['historian.capture-query']), ('16-RELATORIOS.md', 'Relatórios: autoria e operação', ['reports.data-designer', 'reports.layout-variables', 'reports.runtime-output']), ('04-TEMPLATES-E-EQUIPAMENTOS.md', 'Templates e Equipamentos', ['templates.overview', 'equipment.overview']),
              ('13-ALTA-DISPONIBILIDADE.md', 'Alta disponibilidade: configuração e operação', ['ha.configuration-operation']),
              ('14-BANCO-DE-DADOS.md', 'Banco de dados: configuração, migração e recuperação', ['database.topology']),
              ('15-APRESENTACAO-MIDIA-E-LOCAIS.md', 'Apresentação, mídia e locais', ['application.presentation', 'sources.media', 'home-building.organization'])],
    'en': [('18-SECURITY-AND-AUDIT.md', 'Security and Audit: accounts, permissions and protection', ['security.local-accounts', 'security.policy-scopes', 'audit.query-diagnostics', 'security.lock-workflow']), ('17-ALARMS-AND-EVENTS.md', 'Alarms and events: configuration and operation', ['alarms.configuration', 'alarms.runtime-operation', 'operational-events.configuration']), ('07-HISTORIAN-CAPTURE-QUERY.md', 'Historian: capture and queries', ['historian.capture-query']), ('16-REPORTS.md', 'Reports: authoring and operation', ['reports.data-designer', 'reports.layout-variables', 'reports.runtime-output']), ('04-TEMPLATES-AND-EQUIPMENT.md', 'Templates and Equipment', ['templates.overview', 'equipment.overview']),
           ('13-HIGH-AVAILABILITY.md', 'High Availability: configuration and operation', ['ha.configuration-operation']),
           ('14-DATABASE.md', 'Database: configuration, migration and recovery', ['database.topology']),
           ('15-PRESENTATION-MEDIA-LOCATIONS.md', 'Presentation, media and locations', ['application.presentation', 'sources.media', 'home-building.organization'])],
    'es': [('18-SEGURIDAD-Y-AUDITORIA.md', 'Seguridad y auditoría: cuentas, permisos y protección', ['security.local-accounts', 'security.policy-scopes', 'audit.query-diagnostics', 'security.lock-workflow']), ('17-ALARMAS-Y-EVENTOS.md', 'Alarmas y eventos: configuración y operación', ['alarms.configuration', 'alarms.runtime-operation', 'operational-events.configuration']), ('07-HISTORIAN-CAPTURA-CONSULTA.md', 'Historian: captura y consulta', ['historian.capture-query']), ('16-INFORMES.md', 'Informes: diseño y operación', ['reports.data-designer', 'reports.layout-variables', 'reports.runtime-output']), ('04-PLANTILLAS-Y-EQUIPOS.md', 'Plantillas y Equipos', ['templates.overview', 'equipment.overview']),
           ('13-ALTA-DISPONIBILIDAD.md', 'Alta disponibilidad: configuración y operación', ['ha.configuration-operation']),
           ('14-BASE-DE-DATOS.md', 'Base de datos: configuración, migración y recuperación', ['database.topology']),
           ('15-PRESENTACION-MULTIMEDIA-UBICACIONES.md', 'Presentación, multimedia y ubicaciones', ['application.presentation', 'sources.media', 'home-building.organization'])]
}
INTELLIGENCE = {'pt-BR': '12-INTELIGENCIA-ATIVOS-PROCESSOS.md', 'en': '12-ASSET-PROCESS-INTELLIGENCE.md', 'es': '12-INTELIGENCIA-ACTIVOS-PROCESOS.md'}
APPEND_MARKER = '<!-- installed-system-guides -->'

def cell(value):
    return value.replace('|', '&#124;').replace('\n', ' ')

def article(topic, locale):
    text = [f'## {topic["title"]}', '', topic['summary'], '']
    for marker in REVIEW_MARKERS:
        if marker['topicId'] == topic['id']:
            text.extend([f'<!-- {marker["id"]}: https://github.com/brunolrogerio-collab/EliteSCADA/issues/{marker["issue"]}; topic={topic["id"]}; fields={",".join(marker["fieldKeys"])}; status={marker["status"]} -->', ''])
    for section in topic['sections']:
        text.extend([f'### {section["heading"]}', '', re.sub(r'\s+(?=\d{1,2}\.\s)', '\n', section['body']) if section['body'].startswith('1. ') else section['body'], ''])
        if section.get('code'):
            text.extend(['```python', section['code'], '```', ''])
        if section.get('fields'):
            headers = {'pt-BR': ('Campo', 'Para que serve e como preencher', 'Exemplo', 'Limites'),
                       'en': ('Field', 'Purpose and how to fill it', 'Example', 'Bounds'),
                       'es': ('Campo', 'Finalidad y cómo rellenarlo', 'Ejemplo', 'Límites')}[locale]
            text.extend(['| ' + ' | '.join(headers) + ' |', '| --- | --- | --- | --- |'])
            for field in section['fields']:
                text.append('| ' + ' | '.join(cell(field[name]) for name in ('label', 'description', 'example', 'constraints')) + ' |')
            text.append('')
    label = {'pt-BR': 'Abrir este guia na Ajuda instalada', 'en': 'Open this guide in installed Help', 'es': 'Abrir esta guía en Ayuda instalada'}[locale]
    text.extend([f'[{label}](/help?topic={topic["id"]})', ''])
    return '\n'.join(text)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    data = json.loads(SOURCE.read_text(encoding='utf-8'))
    marker_ids = [marker['id'] for marker in REVIEW_MARKERS]
    if len(marker_ids) != len(set(marker_ids)):
        raise SystemExit('Duplicate product-change review marker ID.')
    for locale, topics in data.items():
        by_id = {topic['id']: topic for topic in topics}
        for marker in REVIEW_MARKERS:
            topic = by_id.get(marker['topicId'])
            if topic is None:
                raise SystemExit(f'Missing review topic: {locale}/{marker["topicId"]}')
            field_keys = {field['key'] for section in topic['sections'] for field in section.get('fields') or []}
            if not set(marker['fieldKeys']).issubset(field_keys):
                raise SystemExit(f'Missing review field: {locale}/{marker["id"]}')
    stale = []
    for locale, chapters in CHAPTERS.items():
        topics = {topic['id']: topic for topic in data[locale]}
        for filename, title, ids in chapters:
            path = ROOT / 'docs/manual' / locale / filename
            content = f'# {title}\n\n' + '\n'.join(article(topics[key], locale) for key in ids)
            if args.check:
                if not path.exists() or path.read_text(encoding='utf-8') != content:
                    stale.append(str(path.relative_to(ROOT)))
            else:
                path.write_text(content, encoding='utf-8')
        path = ROOT / 'docs/manual' / locale / INTELLIGENCE[locale]
        original = path.read_text(encoding='utf-8').split(APPEND_MARKER)[0].rstrip()
        content = original + '\n\n' + APPEND_MARKER + '\n\n' + '\n'.join(article(topics[key], locale) for key in
            ('intelligence.activation', 'intelligence.semantics', 'intelligence.analysis-guide'))
        if args.check:
            if path.read_text(encoding='utf-8') != content:
                stale.append(str(path.relative_to(ROOT)))
        else:
            path.write_text(content, encoding='utf-8')
    if stale:
        raise SystemExit('Manual/Help source mismatch: ' + ', '.join(stale))
    total = sum(len(chapters) for chapters in CHAPTERS.values()) + len(INTELLIGENCE)
    print(f'Manual/Help source alignment: {total} files checked.' if args.check else f'Updated {total} localized manual files.')

if __name__ == '__main__':
    main()
