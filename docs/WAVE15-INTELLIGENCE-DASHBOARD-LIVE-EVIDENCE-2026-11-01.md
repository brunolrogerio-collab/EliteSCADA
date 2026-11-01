# Intelligence #791 — painel gerado com persistência real

Conferência local de 01/11/2026 sobre a candidata #854. Dados exclusivamente sintéticos no banco isolado `scadivo_intelligence_browser_853`, TimescaleDB/PostgreSQL em Docker. API normal, JWT exclusivo de testes e licença assinada de teste. Nenhuma rota de API foi simulada. Preview/DEMO não participou.

## Percurso executado

1. A interface real abriu Intelligence e selecionou o perfil de equipamento pelo seletor do gerador.
2. Gerar rascunho abriu uma Tela normal no editor canônico.
3. Pré-visualizar retornou candidato válido; Aplicar usou `POST /api/engineering/import/json/apply` com HTTP 200.
4. O export canônico confirmou a nova Tela e seus vínculos. As APIs existentes salvaram a revisão no PostgreSQL, publicaram e ativaram a aplicação.
5. A aplicação sintética não tinha Home. A primeira abertura retornou `HMI_RUNTIME_STARTUP_SCREEN_REQUIRED`. Configurou-se explicitamente `startupScreenId` pelo Preview/Apply canônico e publicou-se novamente. Isso é requisito da aplicação, sem evidência de defeito do gerador.
6. A revisão final 9 abriu no Runtime a Tela `intelligence-planta.bomba-pump-evidence-2`, com nove objetos e cobertura histórica do perfil.
7. A API foi encerrada e iniciada novamente. A revisão 9, o `startupScreenId`, o modelo da Tela e as nove identidades de objetos permaneceram. O navegador encontrou cada identidade no Runtime.

## Defeito visual encontrado e corrigido na candidata

Os geradores usavam textos claros sobre fundo que seguia o tema global. No tema claro, os títulos e valores ficavam sem contraste adequado. Os rascunhos passam a persistir o fundo escuro correspondente à paleta de seus objetos, usando as propriedades canônicas existentes `backgroundMode=color` e `backgroundColor=#0F172A`. O fundo continua editável pelo autor no editor padrão. As seis entradas do gerador compartilham esse padrão.

Capturas reais em 1440×1000 foram conferidas nos temas claro e escuro. Elas permanecem evidência local, sem substituir o aceite visual do proprietário nem as figuras finais do manual.

## Limites

Esta prova fecha a lacuna de software entre geração, Apply, persistência, publicação, ativação e montagem no Runtime, incluindo reinício. Publicação, ativação e configuração da Home foram realizadas pelas mesmas APIs canônicas usadas pela interface; não se afirma que os cliques desses controles foram auditados aqui. A geração e o Preview/Apply da Tela foram feitos pela interface.

Não houve nova consulta analítica, teste de campo, prognóstico ou KPI agregado. O gráfico sem dados e os valores atuais exibidos não são evidência de qualidade analítica. A validação de consultas reais continua documentada na prova #853. O aceite visual e a integração da cadeia #852/#854 permanecem pendentes.
