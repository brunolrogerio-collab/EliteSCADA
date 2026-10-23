(() => {
  const button = document.querySelector('.language-toggle');
  const nodes = [...document.querySelectorAll('[data-pt][data-en]')];
  const spanish = {
    '· Estação Elevatória de Esgoto': '· Estación de bombeo de aguas residuales',
    '· Reservatórios Hidropneumáticos': '· Depósitos hidroneumáticos',
    '← Voltar ao site SCADIVO': '← Volver al sitio de SCADIVO',
    'Página inicial SCADIVO': 'Página de inicio de SCADIVO', 'Navegação principal': 'Navegación principal',
    'Áreas da plataforma': 'Áreas de la plataforma', 'Plataformas': 'Plataformas', 'Protocolos e integrações': 'Protocolos e integraciones',
    'Capacidades de licenciamento': 'Capacidades de licencias', 'Contato SCADIVO': 'Contacto SCADIVO',
    'Captura da tela de operação da demonstração RHOs no Runtime SCADIVO': 'Captura de la pantalla de operación de la demostración RHOs en SCADIVO Runtime',
    'Captura real da tela de operação da demonstração EEE no Runtime SCADIVO': 'Captura real de la pantalla de operación de la demostración EEE en SCADIVO Runtime',
    'Captura real da demo RHOs no Runtime SCADIVO': 'Captura real de la demostración RHOs en SCADIVO Runtime',
    'Demonstração interativa SCADIVO': 'Demostración interactiva SCADIVO', 'Carregando a demonstração SCADIVO…': 'Cargando la demostración SCADIVO…',
    '01 / RESIDENCIAL': '01 / RESIDENCIAL', '02 / PREDIAL': '02 / EDIFICIOS',
    '1.000 TAGs': '1.000 TAGs', '1.500 TAGs': '1.500 TAGs', '200 TAGs grátis': '200 TAGs gratis', '200 TAGs GRÁTIS': '200 TAGs GRATIS',
    '3.000 TAGs': '3.000 TAGs', '5.000 TAGs': '5.000 TAGs',
    'A capacidade de TAGs, os acessos simultâneos e recursos como redundância e Intelligence são definidos na licença. O portal de compra e emissão automática está em preparação; este site ainda não processa pagamentos.': 'La capacidad de TAGs, los accesos simultáneos y funciones como redundancia e Intelligence se definen en la licencia. El portal de compra y emisión automática está en preparación; este sitio todavía no procesa pagos.',
    'A demonstração está ligada a equipamentos reais?': '¿La demostración está conectada a equipos reales?',
    'Abrir demo unificada · selecionar RHOs': 'Abrir demo unificada · seleccionar RHOs',
    'Abrir demonstração': 'Abrir demostración', 'Abrir Runtime · EEE inicial': 'Abrir Runtime · EEE inicial', 'Acessar demo': 'Abrir demo',
    'Acesso com responsabilidade': 'Acceso con responsabilidad', 'Acompanhamento em tempo real': 'Supervisión en tiempo real',
    'Acompanhe utilidades, água, energia e equipamentos conectados em uma visão organizada da casa.': 'Supervise servicios, agua, energía y equipos conectados en una vista organizada del hogar.',
    'Acompanhe valores, qualidade dos sinais e estados dos equipamentos. Execute comandos e ajuste variáveis conforme as permissões de cada operador.': 'Supervise valores, calidad de señal y estados de los equipos. Ejecute comandos y ajuste variables según los permisos de cada operador.',
    'ALARMES E EVENTOS': 'ALARMAS Y EVENTOS',
    'Além da supervisão, o SCADIVO oferece recursos para automatizar lógicas, analisar o passado e projetar arquiteturas de maior disponibilidade.': 'Además de la supervisión, SCADIVO ofrece herramientas para automatizar lógicas, analizar datos históricos y diseñar arquitecturas de alta disponibilidad.',
    'AMBIENTES DE DEMONSTRAÇÃO': 'ENTORNOS DE DEMOSTRACIÓN', 'ANTES DE COMEÇAR': 'ANTES DE EMPEZAR',
    'Aplicação, comunicação, scripts e histórico no ambiente escolhido para o projeto.': 'Aplicación, comunicación, scripts e historial en el entorno elegido para el proyecto.',
    'Aplicações': 'Aplicaciones', 'AR COMPRIMIDO → VASOS → RAC': 'AIRE COMPRIMIDO → DEPÓSITOS → RAC',
    'Reservatórios hidropneumáticos, compressores, injeção de ar e RAC. Explore o processo, tendências, alarmes e telas mobile em uma aplicação simulada.': 'Depósitos hidroneumáticos, compresores, inyección de aire y RAC. Explora el proceso, las tendencias, las alarmas y las pantallas móviles en una aplicación simulada.',
    'Sim. A implantação contempla Windows, Linux e Docker. Em Edge Computing, o dispositivo precisa ser compatível com os requisitos do sistema e dos protocolos utilizados, incluindo CPU, memória, armazenamento e interfaces de comunicação.': 'Sí. La implementación es compatible con Windows, Linux y Docker. Para Edge Computing, el dispositivo debe cumplir los requisitos del sistema y de los protocolos utilizados, incluidos CPU, memoria, almacenamiento e interfaces de comunicación.',
    'Área do cliente': 'Área de clientes', 'Área responsável': 'Área responsable',
    'As aplicações EEE e RHOs mostram como diferentes processos podem ser representados e acompanhados no SCADIVO. São demonstrações simuladas, sem comunicação com CLP.': 'Las aplicaciones EEE y RHOs muestran cómo representar y supervisar distintos procesos en SCADIVO. Son demostraciones simuladas, sin comunicación con PLC.',
    'Alarmes e eventos': 'Alarmas y eventos', 'Assunto': 'Asunto', 'Até 200 TAGs grátis': 'Hasta 200 TAGs gratis', 'Até 200 TAGs grátis. Espaço para crescer.': 'Hasta 200 TAGs gratis. Espacio para crecer.',
    'Até 200 TAGs gratuitas': 'Hasta 200 TAGs gratis',
    'Até 200 TAGs gratuitas. Supervisão e automação local, web e Edge Computing com Windows, Linux e Docker.': 'Hasta 200 TAGs gratis. Supervisión y automatización local, web y Edge Computing con Windows, Linux y Docker.',
    'ATIVOS VISUAIS DO RUNTIME': 'ACTIVOS VISUALES DEL RUNTIME', 'CAPTURA DO RUNTIME': 'CAPTURA DEL RUNTIME',
    'COMECE A EXPLORAR': 'EMPIEZA A EXPLORAR', 'COMECE GRATUITAMENTE': 'EMPIEZA GRATIS',
    'Comece pequeno. Cresça no mesmo sistema.': 'Empieza con poco. Crece en el mismo sistema.',
    'Comece sua aplicação com até 200 TAGs gratuitas, sem limite de tempo de execução. Quando precisar ampliar a capacidade, evolua o licenciamento e continue no SCADIVO, com a mesma base de engenharia e operação.': 'Empieza tu aplicación con hasta 200 TAGs gratis y sin límite de tiempo de ejecución. Cuando necesites más capacidad, amplía la licencia y continúa con SCADIVO sobre la misma base de ingeniería y operación.',
    'Comercial': 'Ventas', 'Como podemos ajudar?': '¿Cómo podemos ayudarte?',
    'Compare curvas em tempo real e períodos históricos. Consulte valores registrados, qualidade e horários para investigar desvios e compor relatórios operacionais.': 'Compara curvas en tiempo real y períodos históricos. Consulta valores registrados, calidad y horarios para investigar desviaciones y elaborar informes operativos.',
    'Conectividade e TAG Gateway': 'Conectividad y TAG Gateway',
    'Conexões, TAGs, telas, scripts, alarmes, histórico e análise de ativos fazem parte da mesma plataforma. Integradores, projetistas e equipes de operação trabalham com uma estrutura comum, dimensionada para cada aplicação.': 'Conexiones, TAGs, pantallas, scripts, alarmas, historial y análisis de activos forman parte de la misma plataforma. Integradores, proyectistas y equipos de operación trabajan sobre una estructura común, dimensionada para cada aplicación.',
    'Configure condições, prioridades e mensagens. Acompanhe alarmes, reconhecimentos e eventos operacionais para entender a sequência do que aconteceu.': 'Configura condiciones, prioridades y mensajes. Supervisa alarmas, reconocimientos y eventos operativos para entender la secuencia de lo ocurrido.',
    'Conheça os recursos': 'Conoce las funciones', 'Contato': 'Contacto', 'Contato geral': 'Contacto general',
    'Converse com o seu ambiente': 'Interactúa con tu entorno',
    'Crie sinóticos, popups, animações e dínamos reutilizáveis. Combine imagens e objetos ligados às TAGs, com apresentação para desktop e telas mobile dedicadas.': 'Crea sinópticos, ventanas emergentes, animaciones y dínamos reutilizables. Combina imágenes y objetos vinculados a TAGs, con vistas de escritorio y pantallas móviles dedicadas.',
    'Crie, conecte e opere aplicações de automação em um único sistema. Da residência à planta complexa, o que muda é a escala e a lógica do seu projeto. Acesse a engenharia e a operação pelo navegador, localmente ou pela web.': 'Crea, conecta y opera aplicaciones de automatización en un solo sistema. Desde una vivienda hasta una planta compleja, cambian la escala y la lógica del proyecto. Accede a ingeniería y operación desde el navegador, de forma local o por la web.',
    'Da automação residencial à planta complexa.': 'De la automatización residencial a la planta compleja.',
    'Da edição à revisão ativa': 'De la edición a la revisión activa', 'Da engenharia ao acompanhamento da operação.': 'De la ingeniería a la supervisión de la operación.',
    'De 10 a 4.000 caracteres.': 'De 10 a 4.000 caracteres.',
    'De cálculos simples a sequências complexas: escreva Python para automação no servidor e comportamento visual no cliente. Trabalhe com eventos, temporizadores, TAGs e validação no editor.': 'Desde cálculos sencillos hasta secuencias complejas: programa en Python la automatización del servidor y el comportamiento visual del cliente. Trabaja con eventos, temporizadores, TAGs y validación en el editor.',
    'Demonstrações': 'Demostraciones',
    'Disponibilize a aplicação em um servidor próprio, VPS ou infraestrutura de nuvem. Equipes acessam os ambientes autorizados por desktop ou mobile, conforme a configuração da rede e de segurança.': 'Implementa la aplicación en un servidor propio, VPS o infraestructura en la nube. Los equipos acceden a los entornos autorizados desde equipos de escritorio o móviles, según la configuración de red y seguridad.',
    'Dois processos. Uma plataforma.': 'Dos procesos. Una plataforma.', 'E-mail para resposta': 'Correo electrónico para responder',
    'Edifícios': 'Edificios', 'EEE e RHOs são ambientes de demonstração simulados; não representam conexão a CLPs ou equipamentos reais.': 'EEE y RHOs son entornos de demostración simulados; no representan conexiones a PLC ni a equipos reales.',
    'ENGENHARIA': 'INGENIERÍA', 'Engenharia em um só projeto': 'Ingeniería en un solo proyecto',
    'Entre no Runtime sem cadastro. Convidados com credenciais podem explorar a engenharia e experimentar telas e scripts; a aplicação demonstrativa publicada permanece protegida contra salvamento e ativação de alterações.': 'Accede al Runtime sin registrarte. Los invitados con credenciales pueden explorar la ingeniería y probar pantallas y scripts; la aplicación de demostración publicada está protegida para impedir que se guarden o activen cambios.',
    'Enviar mensagem': 'Enviar mensaje', 'Envie sua mensagem': 'Envíanos tu mensaje', 'EQUIPAMENTOS DA DEMO RHOs': 'EQUIPOS DE LA DEMO RHOs',
    'Escolha a área, conte um pouco sobre o que precisa e nossa equipe responderá pelo e-mail informado.': 'Elige un área, cuéntanos qué necesitas y nuestro equipo responderá al correo electrónico indicado.',
    'Escolha onde hospedar o SCADIVO e como acessar a aplicação. O servidor executa o processo; o navegador dá acesso à engenharia e ao Runtime. Operar pela web não obriga você a hospedar na nuvem.': 'Elige dónde alojar SCADIVO y cómo acceder a la aplicación. El servidor ejecuta el proceso y el navegador permite acceder a ingeniería y Runtime. Usar la web no obliga a alojar el sistema en la nube.',
    'Estruture uma arquitetura redundante, acompanhe a disponibilidade dos nós e planeje a continuidade do Runtime. Configure o recurso de acordo com a licença e a topologia do projeto.': 'Diseña una arquitectura redundante, supervisa la disponibilidad de los nodos y planifica la continuidad del Runtime. Configura esta función según la licencia y la topología del proyecto.',
    'Experimentar o SCADIVO': 'Probar SCADIVO', 'FALE COM A NOSSA EQUIPE': 'HABLA CON NUESTRO EQUIPO', 'Fale com a SCADIVO': 'Contacta con SCADIVO',
    'Fale com as equipes Comercial, Suporte ou Contato da SCADIVO.': 'Contacta con los equipos de Ventas, Soporte o Atención general de SCADIVO.',
    'Ilimitado': 'Ilimitado',
    'Instale no ambiente da residência, do edifício ou da fábrica. Acesse pelo navegador na própria máquina ou na rede interna, inclusive em instalações sem conexão permanente com a internet.': 'Instálalo en una vivienda, edificio o fábrica. Accede desde el navegador en el propio equipo o en la red local, incluso en instalaciones sin conexión permanente a Internet.',
    'Integre CLPs, medidores, dispositivos conectados e outras fontes de dados. Use rotas de TAG Gateway para compartilhar valores entre conexões e protocolos.': 'Integra PLC, medidores, dispositivos conectados y otras fuentes de datos. Utiliza rutas de TAG Gateway para compartir valores entre conexiones y protocolos.',
    'Inteligência de ativos e processos, com contexto.': 'Inteligencia de activos y procesos con contexto.', 'LOCAL, WEB E EDGE COMPUTING': 'LOCAL, WEB Y EDGE COMPUTING',
    'Mais profundidade para operações exigentes.': 'Más profundidad para operaciones exigentes.', 'Mensagem': 'Mensaje',
    'Mesma plataforma e mesmo modelo de projeto. A implantação acompanha os requisitos de rede, hardware e disponibilidade da sua operação.': 'La misma plataforma y el mismo modelo de proyecto. La implementación se adapta a los requisitos de red, hardware y disponibilidad de tu operación.',
    'MOBILE': 'MÓVIL', 'Modele processos e utilidades, acompanhe equipamentos, eventos e tendências em operações de maior complexidade.': 'Modela procesos y servicios; supervisa equipos, eventos y tendencias en operaciones de mayor complejidad.',
    'Não. A interface funciona no navegador e pode acessar um servidor na própria máquina, na rede local ou em uma infraestrutura remota. O processamento fica no ambiente escolhido para o projeto; a nuvem é uma opção de implantação.': 'No. La interfaz funciona en el navegador y puede conectarse a un servidor en el mismo equipo, en la red local o en una infraestructura remota. El procesamiento se realiza en el entorno elegido para el proyecto; la nube es una opción de implementación.',
    'Não. EEE e RHOs usam simulações para demonstrar a operação do sistema, com telas reais do Runtime. Em uma instalação, a comunicação é configurada nas fontes de dados e nos drivers correspondentes aos equipamentos do projeto.': 'No. EEE y RHOs utilizan simulaciones para mostrar el funcionamiento del sistema con pantallas reales del Runtime. En una instalación, la comunicación se configura en las fuentes de datos y los controladores adecuados para los equipos del proyecto.',
    'Não. SCADIVO é a mesma plataforma para aplicações residenciais, prediais e industriais. O projeto evolui com mais TAGs, telas, fontes de dados e lógica; a capacidade de hardware, a arquitetura e o licenciamento acompanham esse crescimento.': 'No. SCADIVO es la misma plataforma para aplicaciones residenciales, de edificios e industriales. El proyecto evoluciona con más TAGs, pantallas, fuentes de datos y lógica; la capacidad de hardware, la arquitectura y la licencia acompañan ese crecimiento.',
    'Níveis, bombas submersíveis, alternância e fluxo de processo em um cenário de simulação.': 'Niveles, bombas sumergibles, alternancia y flujo del proceso en un escenario simulado.',
    'No computador ou na rede local': 'En el equipo o en la red local', 'Nome': 'Nombre', 'O começo do seu projeto': 'El comienzo de tu proyecto',
    'O diferencial é reunir engenharia e operação em um fluxo contínuo, com estrutura que pode ser dimensionada do residencial à indústria.': 'La diferencia está en reunir ingeniería y operación en un flujo continuo, con una arquitectura escalable desde una vivienda hasta la industria.',
    'O essencial, sem complicação.': 'Lo esencial, sin complicaciones.', 'O QUE O SISTEMA FAZ': 'QUÉ HACE EL SISTEMA',
    'O que são TAGs e o que significa o limite de 200?': '¿Qué son las TAGs y qué significa el límite de 200?',
    'O SCADIVO é um sistema só. Você utiliza a mesma base de engenharia e operação; o que muda é a quantidade de sinais, as integrações, as telas e a complexidade da aplicação.': 'SCADIVO es un único sistema. Utilizas la misma base de ingeniería y operación; lo que cambia es la cantidad de señales, integraciones, pantallas y la complejidad de la aplicación.',
    'O seu projeto. Onde a operação precisa.': 'Tu proyecto. Donde la operación lo necesita.', 'Onde entra o Python?': '¿Para qué se utiliza Python?',
    'Onde rodar': 'Dónde ejecutarlo',
    'Organize TAGs, fontes de dados, alarmes, eventos e telas em pastas e subpastas. Edite objetos em lote com CSV e transporte a aplicação em um pacote de projeto.': 'Organiza TAGs, fuentes de datos, alarmas, eventos y pantallas en carpetas y subcarpetas. Edita objetos en lote mediante CSV y transporta la aplicación en un paquete de proyecto.',
    'Para integradores de automação, projetistas, fabricantes de máquinas, equipes de manutenção, gestores prediais e operadores de processos. A arquitetura e a capacidade acompanham o projeto.': 'Para integradores de automatización, proyectistas, fabricantes de maquinaria, equipos de mantenimiento, gestores de edificios y operadores de procesos. La arquitectura y la capacidad se adaptan al proyecto.',
    'Pela web, com acesso remoto': 'Por la web, con acceso remoto',
    'Perfis e permissões separam visualização, comandos, alterações de engenharia e administração. A auditoria registra ações para dar rastreabilidade à operação.': 'Los perfiles y permisos separan la visualización, los comandos, los cambios de ingeniería y la administración. La auditoría registra las acciones para garantizar la trazabilidad operativa.',
    'Perto do processo, no Edge': 'Cerca del proceso, en Edge', 'Plantas industriais': 'Plantas industriales',
    'Plataforma': 'Plataforma', 'Playback histórico': 'Reproducción histórica', 'Posso usar Windows, Linux ou um dispositivo Edge?': '¿Puedo usar Windows, Linux o un dispositivo Edge?',
    'Preciso trocar de sistema quando a aplicação crescer?': '¿Tengo que cambiar de sistema cuando crezca la aplicación?',
    'Prepare mudanças na engenharia, salve revisões, publique e ative a versão que vai operar. O projeto reúne os objetos, suas referências e a organização de pastas.': 'Prepara cambios de ingeniería, guarda revisiones, publica y activa la versión que estará en funcionamiento. El proyecto reúne los objetos, sus referencias y la estructura de carpetas.',
    'PROCESSO DE BOMBEAMENTO': 'PROCESO DE BOMBEO', 'PROCESSO HIDROPNEUMÁTICO': 'PROCESO HIDRONEUMÁTICO',
    'Protocolos e integrações para automação residencial, predial e industrial, por meio das fontes de dados do SCADIVO.': 'Protocolos e integraciones para automatización residencial, de edificios e industrial mediante las fuentes de datos de SCADIVO.',
    'Qual é o assunto?': '¿Cuál es el asunto?', 'RECURSOS AVANÇADOS': 'FUNCIONES AVANZADAS',
    'Redundância e alta disponibilidade': 'Redundancia y alta disponibilidad',
    'Relacione equipamentos, etapas e perfis de sinais (TAGs) para consultar cobertura, indicadores e comparações baseados em dados registrados — com fórmulas, unidades, períodos e origem identificados. Se faltarem dados, o sistema informa a limitação em vez de estimar um valor.': 'Relaciona equipos, etapas y perfiles de señales (TAGs) para consultar cobertura, indicadores y comparaciones basados en datos registrados, con fórmulas, unidades, períodos y origen identificados. Si faltan datos, el sistema informa la limitación en lugar de estimar un valor.',
    'Residencial': 'Residencial',
    'Residencial, predial e industrial. Local, web e Edge.': 'Residencial, edificios e industria. Local, web y Edge.',
    'Reúna subsistemas como climatização, bombeamento, reservatórios e energia, com alarmes e histórico.': 'Integra subsistemas como climatización, bombeo, depósitos y energía, con alarmas e historial.',
    'SCADA & Automação em qualquer escala': 'SCADA y automatización a cualquier escala',
    'SCADIVO — SCADA & Automação em qualquer escala': 'SCADIVO — SCADA y automatización a cualquier escala',
    'SCADIVO — Um só SCADA. Da casa à indústria.': 'SCADIVO — Un solo SCADA. Del hogar a la industria.',
    'SCADIVO INTELLIGENCE': 'SCADIVO INTELLIGENCE',
    'SCADIVO: um só SCADA para casas, edifícios e indústrias. Até 200 TAGs gratuitas, acesso local ou web, Windows, Linux e Docker para Edge Computing.': 'SCADIVO: un solo SCADA para hogares, edificios e industrias. Hasta 200 TAGs gratis, acceso local o web, Windows, Linux y Docker para Edge Computing.',
    'Scripts em Python': 'Scripts en Python',
    'Scripts permitem implementar cálculos, sequências e reações a eventos no servidor, além de comportamentos e animações visuais no cliente. O editor oferece validação e os scripts utilizam as APIs e permissões disponibilizadas pelo SCADIVO.': 'Los scripts permiten implementar cálculos, secuencias y respuestas a eventos en el servidor, además de comportamientos y animaciones visuales en el cliente. El editor ofrece validación y los scripts utilizan las API y permisos disponibles en SCADIVO.',
    'SEM CLP': 'SIN PLC', 'SIMULAÇÃO · SEM CLP': 'SIMULACIÓN · SIN PLC', 'SIMULADA': 'SIMULADA',
    'Sinóticos, popups e mobile': 'Sinópticos, ventanas emergentes y móvil',
    'SUPERVISÃO E AUTOMAÇÃO EM QUALQUER ESCALA': 'SUPERVISIÓN Y AUTOMATIZACIÓN A CUALQUIER ESCALA',
    'Suporte': 'Soporte', 'TAGs gratuitas': 'TAGs gratis',
    'TAGs são variáveis utilizadas pela aplicação, como temperatura, nível, pressão, estado de uma bomba ou um comando. A faixa gratuita contempla aplicações de até 200 TAGs. Para uma aplicação maior, escolha uma licença com a capacidade necessária.': 'Las TAGs son variables utilizadas por la aplicación, como temperatura, nivel, presión, estado de una bomba o un comando. El plan gratuito contempla aplicaciones de hasta 200 TAGs. Para una aplicación mayor, elige una licencia con la capacidad adecuada.',
    'TEMPO REAL': 'TIEMPO REAL', 'TENDÊNCIAS': 'TENDENCIAS', 'Tendências e histórico': 'Tendencias e historial',
    'UM MESMO SISTEMA': 'UN MISMO SISTEMA', 'Um só SCADA. Da casa à indústria.': 'Un solo SCADA. Del hogar a la industria.',
    'UMA PLATAFORMA QUE ACOMPANHA A ESCALA': 'UNA PLATAFORMA QUE CRECE CONTIGO',
    'Usaremos seu nome, e-mail e mensagem somente para encaminhar e responder esta solicitação. Não envie senhas ou chaves. Este formulário não cria uma conta SCADIVO.': 'Usaremos tu nombre, correo electrónico y mensaje únicamente para tramitar y responder a esta solicitud. No envíes contraseñas ni claves. Este formulario no crea una cuenta SCADIVO.',
    'Use Docker em computadores industriais e dispositivos Edge compatíveis. Mantenha aquisição e processamento próximos dos equipamentos, dimensionando CPU, memória e armazenamento para a aplicação.': 'Usa Docker en computadoras industriales y dispositivos Edge compatibles. Mantén la adquisición y el procesamiento cerca de los equipos y dimensiona la CPU, la memoria y el almacenamiento según la aplicación.',
    'Veja o mesmo sistema em duas aplicações.': 'Conoce el mismo sistema en dos aplicaciones.',
    'Você receberá uma confirmação automática com o protocolo.': 'Recibirás una confirmación automática con el número de referencia.',
    'Volte a um instante do processo e acompanhe os valores históricos no próprio sinótico. Investigue ocorrências com o contexto visual da operação, em modo somente leitura.': 'Vuelve a un instante del proceso y consulta los valores históricos en el propio sinóptico. Investiga lo ocurrido con el contexto visual de la operación, en modo de solo lectura.',
    'Web significa que tudo precisa estar na nuvem?': '¿Usar la web significa que todo debe estar en la nube?'
  };
  let language = 'pt';

  function applyLanguage(next) {
    language = next;
    document.documentElement.lang = language === 'pt' ? 'pt-BR' : language;
    for (const node of nodes) {
      const value = language === 'es' ? (spanish[node.dataset.pt] || node.dataset.en) : node.dataset[language];
      if (node instanceof HTMLMetaElement) node.content = value;
      else if (node.dataset.translateAttribute) node.setAttribute(node.dataset.translateAttribute, value);
      else node.textContent = value;
    }
    if (button) {
      button.textContent = language === 'pt' ? 'EN' : language === 'en' ? 'ES' : 'PT';
      const aria = language === 'es' ? 'Cambiar idioma a portugués' : language === 'en' ? 'Switch language to Spanish' : 'Mudar idioma para inglês';
      button.setAttribute('aria-label', aria);
    }
    try { localStorage.setItem('scadivo-site-language', language); } catch { /* Keep language switching available when storage is blocked. */ }
    document.dispatchEvent(new CustomEvent('scadivo:languagechange', { detail: { language } }));
  }

  button?.addEventListener('click', () => applyLanguage(language === 'pt' ? 'en' : language === 'en' ? 'es' : 'pt'));
  let saved;
  try { saved = new URLSearchParams(location.search).get('lang') || localStorage.getItem('scadivo-site-language'); } catch { /* Portuguese remains the default. */ }
  applyLanguage(['en', 'es'].includes(saved) ? saved : 'pt');
})();
