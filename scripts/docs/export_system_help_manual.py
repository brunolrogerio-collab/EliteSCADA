"""Keep long-form system chapters aligned with installed localized Help content."""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'src/Scada.Api/Runtime/HelpGuides/system-guides.json'
CHAPTERS = {
    'pt-BR': [('04-TEMPLATES-E-EQUIPAMENTOS.md', 'Templates e Equipamentos', ['templates.overview', 'equipment.overview']),
              ('13-ALTA-DISPONIBILIDADE.md', 'Alta disponibilidade: configuração e operação', ['ha.configuration-operation']),
              ('14-BANCO-DE-DADOS.md', 'Banco de dados: configuração, migração e recuperação', ['database.topology']),
              ('15-APRESENTACAO-MIDIA-E-LOCAIS.md', 'Apresentação, mídia e locais', ['application.presentation', 'sources.media', 'home-building.organization'])],
    'en': [('04-TEMPLATES-AND-EQUIPMENT.md', 'Templates and Equipment', ['templates.overview', 'equipment.overview']),
           ('13-HIGH-AVAILABILITY.md', 'High Availability: configuration and operation', ['ha.configuration-operation']),
           ('14-DATABASE.md', 'Database: configuration, migration and recovery', ['database.topology']),
           ('15-PRESENTATION-MEDIA-LOCATIONS.md', 'Presentation, media and locations', ['application.presentation', 'sources.media', 'home-building.organization'])],
    'es': [('04-PLANTILLAS-Y-EQUIPOS.md', 'Plantillas y Equipos', ['templates.overview', 'equipment.overview']),
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
    for section in topic['sections']:
        text.extend([f'### {section["heading"]}', '', re.sub(r'\s+(?=\d{1,2}\.\s)', '\n', section['body']) if section['body'].startswith('1. ') else section['body'], ''])
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
    print('Manual/Help source alignment: 15 files checked.' if args.check else 'Updated 15 localized manual files.')

if __name__ == '__main__':
    main()
