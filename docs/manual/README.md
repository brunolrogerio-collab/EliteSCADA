# EliteSCADA — Manual completo

Este diretório é a documentação de produto de longa duração para usuários, engenheiros de automação, autores HMI e de Scripts, administradores, responsáveis por segurança/licenciamento, operação e manutenção.

A Ajuda contextual do produto é curta e orientada à tarefa. Este manual aprofunda os mesmos conceitos e usa, quando disponível, links por Topic ID para `/help?topic=<id>`.

## Organização

- `pt-BR/`: conteúdo-base de autoria da Fase 1.
- `en/` e `es/`: estrutura de paridade preparada; tradução integral ocorre de forma controlada.
- `COVERAGE-MATRIX.md`: matriz editorial por capítulo, dependências e estado de screenshot.

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

## Conteúdo de Fase 1

O núcleo pt-BR já contém fundamentos e procedimentos estáveis para conceitos de produto, lifecycle, dados/TAGs, scripting, alarmes/histórico fundamental, operação, segurança, licenciamento, portabilidade, recovery e troubleshooting. Superfícies ainda em mudança permanecem deliberadamente estruturadas na matriz, sem passos de UI inventados.
