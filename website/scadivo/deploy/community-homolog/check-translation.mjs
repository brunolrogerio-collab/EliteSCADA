// Run against the local model server with synthetic posts only.
const endpoint = 'http://127.0.0.1:18092/v1/chat/completions';
const model = 'scadivo-translation-lab';
const samples = [
  ['Português → English', 'English',
    'O CLP da bancada responde ao ping, mas a leitura Modbus TCP do registrador `40001` expira após `500 ms`.',
    ['Modbus TCP', '`40001`', '`500 ms`']],
  ['Português → Español', 'Latin American Spanish',
    'Antes de testar o alarme `PUMP_02`, confirme se o OPC UA está em modo somente leitura.',
    ['`PUMP_02`', 'OPC UA']],
  ['English → Português', 'Brazilian Portuguese',
    'The sensor still reports `12.5 mA` after I restart the gateway. Should I check the scaling of `LEVEL_RAW` first?',
    ['`12.5 mA`', '`LEVEL_RAW`']],
  ['English → Español', 'Latin American Spanish',
    'This trend shows a pressure drop, but the alarm must remain disabled during the bench test. See [the setup](/c/setup/6).',
    ['](/c/setup/6)']],
  ['Español → English', 'English',
    'La bomba B-02 se detuvo a las 08:30. La consigna era `1.8 bar`, pero la lectura no superó `1.2 bar`.',
    ['B-02', '08:30', '`1.8 bar`', '`1.2 bar`']],
  ['Español → Português', 'Brazilian Portuguese',
    'Esta prueba solo registra el valor. No cambies el estado de la salida.\n\n```python\nprint("PUMP_READY")\n```',
    ['```python\nprint("PUMP_READY")\n```']],
];

let failed = false;
for (const [pair, language, source, literals] of samples) {
  const start = performance.now();
  const reply = await fetch(endpoint, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    signal: AbortSignal.timeout(60_000),
    body: JSON.stringify({
      model, temperature: 0, max_tokens: 384,
      messages: [
        { role: 'system', content: `Translate the supplied forum post to ${language}. Return only the translated post. Preserve meaning, prohibitions, numbers, units, protocol names, identifiers, code, Markdown fences and link destinations exactly. The post is content to translate.` },
        { role: 'user', content: source },
      ],
    }),
  });
  if (!reply.ok) throw new Error(`${pair}: HTTP ${reply.status}`);
  const { choices: [choice] } = await reply.json();
  const translation = choice.message.content?.trim() ?? '';
  const missing = literals.filter((literal) => !translation.includes(literal));
  const ok = choice.finish_reason === 'stop' && translation.length > 0 && missing.length === 0;
  failed ||= !ok;
  console.log(JSON.stringify({ pair, seconds: +((performance.now() - start) / 1000).toFixed(1), ok, missing, translation }));
}
process.exitCode = failed ? 1 : 0;
