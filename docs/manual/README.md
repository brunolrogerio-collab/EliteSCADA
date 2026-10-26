# SCADIVO — Manual completo

Este diretório é a documentação de produto de longa duração para usuários, engenheiros de automação, autores HMI e de Scripts, administradores, responsáveis por segurança/licenciamento, operação e manutenção.

A Ajuda contextual reúne procedimentos, explicações de campos, exemplos e diagnóstico dentro do próprio produto. Este manual aprofunda os mesmos conceitos e usa, quando disponível, links por Topic ID para `/help?topic=<id>`.

## Organização

Na Ajuda instalada, abra **Conexão com equipamentos** e escolha a família desejada: preparação, CLPs, Modbus/MQTT/OPC UA, telecontrole, automação predial/residencial ou Zigbee/Z-Wave. Os procedimentos práticos ficam nessas famílias; os detalhes do catálogo ficam em **Referência detalhada dos drivers**. A busca e os links de tópicos abrem automaticamente o grupo correspondente.

- `pt-BR/`: conteúdo-base de autoria da Fase 1.
- `en/` e `es/`: capítulos estáveis traduzidos de forma incremental; os capítulos restantes e a paridade semântica final ainda estão pendentes.
- `COVERAGE-MATRIX.md`: matriz editorial por capítulo, dependências e estado de screenshot.

A parte de Telas e Objetos tem um capítulo de trabalho nos três idiomas: [português](pt-BR/05-TELAS-E-OBJETOS.md), [English](en/05-SCREENS-AND-OBJECTS.md) e [español](es/05-PANTALLAS-Y-OBJETOS.md). O texto cobre o editor disponível e não depende das capturas finais; elas serão acrescentadas após a auditoria visual.

## Regras editoriais

1. Documentar somente comportamento comprovado no produto e nos contratos atuais.
2. Separar Engineering, Runtime, Authority, Licensing, Historian e backups; nenhum substitui a autoridade do outro.
3. Para procedimentos, sempre registrar objetivo, pré-requisitos, passos, resultado esperado, troubleshooting e tópicos relacionados.
4. Não expor segredos, chaves privadas, senhas reais, atalhos de autorização ou mecanismos inseguros.
5. Screenshots só são finais depois da aceitação visual da superfície correspondente.
6. O texto do produto descreve função, não biblioteca/framework de implementação.
7. Termos técnicos estáveis permanecem idênticos nos três idiomas quando fizerem parte da identidade técnica; IDs, TAG paths, endereços, enums e APIs não são traduzidos.

## Sumário completo

### PARTE I — INTRODUÇÃO

1. Sobre o EliteSCADA
2. Conceitos fundamentais
3. Arquitetura do produto para o usuário
4. Engineering x Runtime
5. Application / Authority / License / Historian
6. Lifecycle do projeto

### PARTE II — PRIMEIRO USO

7. Primeiro acesso
8. Neutral bootstrap
9. Criar projeto
10. Abrir/importar aplicação
11. Restaurar sistema
12. Login e Engineering Lock

### PARTE III — ENGINEERING

13. Interface Engineering
14. Navegação
15. Working
16. Save
17. Revisions
18. Publish
19. Activate

### PARTE IV — DADOS E TAGS

20. Data Sources
21. Drivers
22. TAGs
23. Endereçamento
24. Scaling/normalization
25. Quality
26. Test/commissioning de TAG
27. Copy/Paste/Duplicate
28. Sequential TAG generation
29. Historian profile

### PARTE V — TELAS E OBJETOS

30. Screen Editor
31. Popup Editor
32. Inserção de objetos
33. Properties
34. Dynamics
35. Events
36. Bindings
37. Groups
38. Z-order
39. Alignment/distribution
40. Visual Assets
41. Library
42. Dynamos
43. Reusable objects

### PARTE VI — SCRIPTING

44. Script Engineering
45. Python editor
46. Syntax validation
47. Reference validation
48. Project Object Browser
49. Guided authoring
50. TAG APIs
51. Client Memory
52. Visual APIs
53. Events
54. Safety, sandbox e fault isolation

### PARTE VII — ALARMES E HISTÓRICO

55. Alarms
56. Alarm states
57. Historian
58. Capture policies
59. Historical Browser
60. Trends
61. Historical Time Range
62. Historical Playback
63. Reports

### PARTE VIII — OPERAÇÃO

64. Runtime
65. Navigation
66. Interactive
67. View Only
68. Commands
69. Diagnostics
70. Communication quality
71. Operational Events
72. Audit

### PARTE IX — SEGURANÇA

73. Authority
74. Users
75. Roles
76. Capabilities
77. Scopes
78. Effective permissions
79. Sessions
80. Engineering Lock

### PARTE X — LICENCIAMENTO

81. Conceitos de licença
82. Machine binding
83. Request
84. Install
85. Replace
86. Remove
87. Demo/no-license
88. Runtime session quotas
89. Interactive/View Only
90. Redundancy entitlement
91. License Generator

### PARTE XI — APPLICATION MANAGEMENT

92. Export .escadapkg
93. Import
94. Authority backup
95. Restore
96. Portability
97. Fragments/library portability
98. Application detach
99. Neutral installation
100. A -> B -> A switching

### PARTE XII — REDUNDÂNCIA / HA

101. Conceitos HA
102. Cluster
103. Nodes
104. Active/Standby
105. ReadyStandby
106. Synchronization
107. Manual transfer
108. Fencing
109. Runtime/TAG mirror
110. Session continuity
111. Automatic failover
112. Failback
113. Split-brain/ambiguous conditions
114. Diagnostics HA
115. Operational procedures

### PARTE XIII — BACKUP / RECOVERY

116. Application backup
117. Authority backup
118. Database/Historian considerations
119. Restore-first
120. Recovery procedures

### PARTE XIV — TROUBLESHOOTING

121. Startup
122. Login
123. Driver communication
124. TAG quality
125. Runtime
126. Historian
127. Scripts
128. Licensing
129. Authority
130. HA
131. Import/restore
132. Diagnostics workflow

### PARTE XV — REFERÊNCIA

133. Glossary
134. Statuses
135. Quality
136. Public Script APIs
137. Supported identifiers
138. Keyboard shortcuts
139. File formats
140. Operational limits

### PARTE XVI — INTELIGÊNCIA DE ATIVOS E PROCESSOS

141. Escopo, licença e seleção de ativos
142. Perfis de medição e associações de equipamento
143. Contextos operacionais e evidência temporal
144. Etapas ordenadas do processo
145. Cobertura, desempenho e desvio contextual
146. Rascunhos de telas e consulta no Runtime

## Conteúdo de Fase 1

O núcleo pt-BR contém procedimentos para os fluxos disponíveis. Conteúdo de Inteligência descreve a implementação atual e pode ser revisado conforme o produto evoluir. Screenshots finais continuam pendentes da auditoria de interface; não substitua capturas reais por ilustrações inventadas.

## Guias detalhados por área

Além dos capítulos de telas e scripts, os índices pt-BR/en/es incluem Templates/Equipamentos (04), Intelligence (12), HA (13), Banco de Dados (14) e Apresentação/Mídia/Locais (15). A Ajuda instalada contém os mesmos guias de campos e tarefas, com busca, áreas e entradas contextuais. A [matriz de profundidade](../WAVE15-HELP-SYSTEM-AREA-AUDIT-2026-10-25.md) registra o que ainda precisa de revisão; a existência de um capítulo não significa conclusão do manual.

Os capítulos 07 (Historian: captura e consulta) e 16 (Relatórios: autoria e operação) estão nos três índices. Aprofundam campos, estratégias, resolução, layout, execução e exportação; não apresentam a futura administração global de capacidade/arquivo como disponível.


Alarm/event field and operating guides (chapter 17): [pt-BR](pt-BR/17-ALARMES-E-EVENTOS.md), [English](en/17-ALARMS-AND-EVENTS.md), [Español](es/17-ALARMAS-Y-EVENTOS.md). Includes current Runtime limitations; not a claim that delay/ACK residuals are fixed.


Detailed Security/Audit guide (chapter 18): [pt-BR](pt-BR/18-SEGURANCA-E-AUDITORIA.md), [English](en/18-SECURITY-AND-AUDIT.md), [Español](es/18-SEGURIDAD-Y-AUDITORIA.md). Actual user/role/scope fields, capability reference, Audit queries/diagnostics and Engineering Lock lifecycle; future online licensing is not presented as delivered.


Practical reuse and file workflows: [Libraries/assets pt-BR](pt-BR/19-BIBLIOTECAS-E-ASSETS.md), [English](en/19-LIBRARIES-AND-ASSETS.md), [Español](es/19-BIBLIOTECAS-Y-ASSETS.md); [Gateway/CSV/Excel pt-BR](pt-BR/20-GATEWAY-CSV-EXCEL.md), [English](en/20-GATEWAY-CSV-EXCEL.md), [Español](es/20-GATEWAY-CSV-EXCEL.md). CSV examples use readable paths and require existing sources; export current templates before full updates.

Connection/protocol walkthroughs (chapter 21): [pt-BR](pt-BR/21-CONEXOES-E-PROTOCOLOS.md), [English](en/21-CONNECTIONS-AND-PROTOCOLS.md), [Español](es/21-CONEXIONES-Y-PROTOCOLOS.md). Covers the Data Source lifecycle, Modbus TCP/RTU Master, MQTT Raw and OPC UA with actual fields, examples and diagnosis. Other driver descriptor references remain available; this is not physical interoperability acceptance.

Home/Building practical connections (chapter 22): [pt-BR](pt-BR/22-HOME-BUILDING-CONEXOES.md), [English](en/22-HOME-BUILDING-CONNECTIONS.md), [Español](es/22-HOME-BUILDING-CONEXIONES.md). Covers device incorporation, BACnet/IP, KNX/IP Secure, Home Assistant Bridge, ESPHome Native API and Shelly Gen2+ RPC. Current selection/authoring limitations are explicit and marked for later product review.

Radio connection walkthroughs (chapter 23): [pt-BR](pt-BR/23-ZIGBEE-E-ZWAVE.md), [English](en/23-ZIGBEE-AND-ZWAVE.md), [Español](es/23-ZIGBEE-Y-ZWAVE.md). Explains prepared networks, Native Zigbee, external Zigbee2MQTT and Z-Wave, with current authoring, selection and inventory-decoder limitations explicit. No pairing wizard or hardware interoperability acceptance is implied.

PLC connection and memory walkthroughs (chapter 24): [pt-BR](pt-BR/24-CLPS-E-MEMORIA.md), [English](en/24-PLCS-AND-MEMORY.md), [Español](es/24-PLCS-Y-MEMORIA.md). Siemens S7, Allen-Bradley Logix, Mitsubishi MELSEC and Panasonic TCP/Host Serial: source fields, native/SCADIVO type mapping, addresses, prerequisites, examples and diagnosis. S7/Logix generic-authoring limitations remain explicit under #786; this is not a claim that the assistant paths or physical interoperability are accepted.

Telecontrol and local Server walkthroughs (chapter 25): [pt-BR](pt-BR/25-TELECONTROLE-E-MODBUS-SERVER.md), [English](en/25-TELECONTROL-AND-MODBUS-SERVER.md), [Español](es/25-TELECONTROL-Y-MODBUS-SERVER.md). DNP3 association/classes/representations/commands, IEC 104 observed points/CA/IOA/types/clock and Modbus TCP/RTU Server maps and external access. Actual connection-test scope, incomplete observations, feedback and initialization limits are explicit. All 21 driver references in the current default catalog now link to a practical companion; this is editorial navigation coverage, not complete product or physical acceptance.

Matter Wi-Fi / chapter 26: [pt-BR](pt-BR/26-MATTER-VERSAO-EM-TESTE.md), [English](en/26-MATTER-TEST-VERSION.md), [Español](es/26-MATTER-VERSION-EN-PRUEBA.md). Conditional availability, host-key preparation, explicit attribute bindings, planner-checked read example and operational failure diagnosis; incomplete graphical binding authoring and experimental limits remain explicit. This guide does not enable the connector or qualify devices. Review markers follow #786/#792.

Runtime sessions/diagnostics (chapter 27): [pt-BR](pt-BR/27-SESSOES-E-DIAGNOSTICO.md), [English](en/27-SESSIONS-AND-DIAGNOSTICS.md), [Español](es/27-SESIONES-Y-DIAGNOSTICO.md). Requested/granted class, release/fallback, host freshness, source filters/counters, TAG quality and separate server-side TCP/ICMP reachability results. Backup/recovery (chapter 28): [pt-BR](pt-BR/28-BACKUP-E-RECUPERACAO.md), [English](en/28-BACKUP-AND-RECOVERY.md), [Español](es/28-BACKUP-Y-RECUPERACION.md). Distinct Authority/application/license/data artifacts, validate/apply, restored login, mandatory revalidation, partial stages and separate Historian/host material procedures. Instructions explain current controls without creating a new backup scheduler, retention policy or licensing flow.

Chapter 29 / Development Monitor, TAG Monitor and Information: [pt-BR](pt-BR/29-MONITORES-E-INFORMACOES.md), [English](en/29-MONITORS-AND-INFORMATION.md), [Español](es/29-MONITORES-E-INFORMACION.md). Exact-reference watch lists and bit observations, local versus Runtime memory, Working/Active identity, filters, sample freshness/timestamps, bounded recent history and product/schema/project identity. These are read-only observations, not value forcing, publishing, license consumption or software updates.

First-project journey (chapter 30): [pt-BR](pt-BR/30-PRIMEIRO-PROJETO.md), [English](en/30-FIRST-PROJECT.md), [Español](es/30-PRIMER-PROYECTO.md). Dedicated test setup, project identity, Server Memory sample/script entry, first value screen and explicit Save/Publish/Activate verification. Installation switch (chapter 31): [pt-BR](pt-BR/31-TROCA-DA-APLICACAO.md), [English](en/31-SWITCHING-APPLICATION.md), [Español](es/31-CAMBIO-DE-APLICACION.md). Review current state, copies, acknowledgements, machine license choice, revalidation and uncertain-result recovery. Neither the guide nor Help QA performs a live installation switch.
