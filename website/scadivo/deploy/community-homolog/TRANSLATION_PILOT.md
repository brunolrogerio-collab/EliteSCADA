# Piloto de tradução da Comunidade SCADIVO

## Localização dos tópicos iniciais

O [pacote revisado](initial-topic-localizations.json) contém títulos e corpos em inglês e espanhol para **Regras da comunidade SCADIVO** e **Boas-vindas à comunidade SCADIVO**. O original em português continua sendo a publicação de referência. Os IDs e links do pacote pertencem a `community.scadivo.com`. Em homologação, mapear pelo título original e conferir os links no ambiente de destino.

A versão publicada, 2026.1.4 (`2c3ac01e`), dispõe da localização nativa. Habilitar **Content localization**, manter `pt_BR` como padrão e incluir `en` e `es` nos idiomas possíveis. Usar as opções de localização do título e da primeira publicação. Conferir o idioma de origem de ambos antes de cadastrar as traduções. Fazer backup das traduções existentes antes de editar e manter **AI translation** desabilitada. O seletor do tema SCADIVO já existe. Se ele atender aos visitantes e aos perfis, manter apenas esse seletor.

Antes de publicar, validar a conta de teste entrando pelo Portal com cada idioma do perfil. Conferir o título na lista, o corpo do tópico, o controle para ver o original e o retorno a português. Para visitante, conferir a seleção de idioma e os links de categorias. Para reversão, retirar as localizações novas ou restaurar as anteriores sem editar o texto original.

## Categorias públicas

O [pacote de categorias](category-localizations.json) contém os nomes e as descrições das oito categorias públicas em inglês e espanhol. As traduções são dadas à localização nativa, vinculadas ao endereço estável de cada categoria. O português permanece como referência e o pacote preserva o link das regras.

Na versão 2026.1.4, a página de categorias usa um campo de resumo distinto da descrição localizada. O plugin `scadivo-visitor-locale` entrega o resumo localizado nessa versão. Versões com suporte nativo ao resumo usam seu próprio tratamento. Pedidos de troca explícita de idioma passam fora do cache de visitantes para gravar a preferência. O ajuste preserva permissões, endereços e tópicos.

Com a localização de conteúdo já habilitada, execute `bundle exec rails runner apply-category-localizations.rb category-localizations.json` no diretório da aplicação, apontando para as cópias dos arquivos no ambiente escolhido. O script confere os oito textos de origem e a visibilidade pública antes de gravar as localizações em uma transação. Faça uma cópia de segurança das localizações e da imagem antes da publicação.

Importe [SCADIVO Mobile Category Descriptions](theme-components/mobile-category-descriptions/) como componente e acrescente-o ao tema ativo. O componente exibe a descrição que a lista móvel já recebe da localização nativa. Os outros componentes do tema, o desktop e as categorias silenciadas preservam o comportamento atual. Para reverter, remova a associação do componente com o tema. Valide em janelas anônimas com PT, EN e ES no desktop e no celular, troque o idioma no seletor e recarregue o catálogo.

Com as dependências de navegador instaladas, execute `node tests/browser/check-category-localizations.mjs <URL-da-Comunidade>` a partir de `website/scadivo` para repetir as verificações anônimas.

O teste local em `eafa0d12` usou os serviços nativos de criação e seleção de localizações. Os dois tópicos sintéticos passaram em português, inglês e espanhol (6/6), com links preservados. Os dados de teste foram revertidos ao fim da transação. A navegação autenticada e a publicação das traduções no domínio real ainda precisam ser verificadas.

## Interface da Comunidade e página de perguntas frequentes

Inclua o plugin [scadivo-interface-localization](plugins/scadivo-interface-localization/) na imagem da Comunidade. As traduções de interface corrigem os atalhos do chat, a seleção em massa de tópicos e os rótulos de acessibilidade da navegação e dos títulos. A página `/faq` usa a publicação localizada do tópico configurado como Regras, com retorno ao original quando a tradução estiver indisponível ou o usuário escolher ver o texto original. O plugin também localiza a descrição padrão em Sobre e resolve os nomes das medalhas Editor e Primeiro link pelo idioma atual, inclusive em notificações antigas.

Valide com o tópico de Regras localizado e uma conta de teste confirmada no Portal. O [verificador móvel](../../tests/browser/check-interface-localizations.mjs) usa dois endereços de homologação no loopback e as variáveis `SCADIVO_TEST_USERNAME` e `SCADIVO_TEST_PASSWORD`. Use a conta sintética da equipe com uma notificação da medalha Primeiro link criada em português e as regras de teste com seis itens. O teste entra pelo Portal, usa um idioma de navegador diferente do perfil, abre a FAQ pelo menu e repete a conferência após recarregar.

A publicação requer uma imagem que inclua o plugin e uma cópia protegida da imagem anterior. Depois da troca, limpe o cache anônimo e o cache de traduções pelo mecanismo nativo. A reversão restaura a imagem anterior. Os textos dos canais e as publicações internas continuam sendo conteúdo editável do fórum.

## Canais e área da equipe

O plugin [scadivo-channel-localization](plugins/scadivo-channel-localization/) acrescenta os rótulos localizados à resposta de chat quando o leitor tem acesso ao canal. O componente [SCADIVO Localized Chat Channels](theme-components/localized-chat-channels/) exibe esses rótulos no menu, no catálogo e no cabeçalho do canal. A edição continua usando o título e a descrição originais. Os endereços dos canais e as permissões seguem os valores cadastrados.

O arquivo de traduções da equipe fica no registro operacional protegido. O Git contém apenas o instalador e o roteiro de teste. O [instalador](apply-staff-localizations.rb) usa as localizações nativas para a categoria restrita e para o título e a primeira publicação de cada tópico. Ele confere a origem e a visibilidade antes de gravar em uma transação. A reaplicação do mesmo pacote é segura. Se o original, uma tradução cadastrada ou a visibilidade tiver mudado, revise o pacote antes de aplicar novamente.

Antes de publicar, preserve o ajuste de inicialização da imagem usada na #917 e faça backup do banco e da associação dos componentes do tema. Inclua o plugin na imagem, associe o novo componente ao tema e aplique o arquivo privado com `bundle exec rails runner apply-staff-localizations.rb <arquivo-privado>`. Para reverter, volte à imagem anterior, desassocie apenas o novo componente e restaure os registros de localização desta etapa a partir do backup.

O [roteiro móvel](../../tests/browser/check-staff-localizations.mjs) recebe a URL do Portal, a URL do fórum e o arquivo privado de homologação. Use `SCADIVO_TEST_USERNAME` e `SCADIVO_TEST_PASSWORD` com a conta sintética. Execute primeiro com acesso de equipe nos três idiomas. Para a rodada de permissões, use uma conta comum que siga o canal público e informe em `SCADIVO_TEST_RESTRICTED_PATHS` as rotas de teste obtidas na primeira rodada.

## Tradução automática de contribuições

O motor nativo guarda traduções para reutilizar nas leituras. No commit em produção, a opção `ai_translation_backfill_limit_to_public_content` restringe a rotina de tradução de publicações a conteúdo público, incluindo a exclusão de mensagens privadas. A seleção de categorias por lista só está disponível em código posterior. Para um piloto de uma categoria, usar uma instância isolada com dados sintéticos ou validar uma versão com essa opção. Sem tradução de mensagens privadas, categorias restritas, chamados ou histórico nesse piloto.

O ensaio usou [Qwen3 1.7B Q8_0](https://huggingface.co/Qwen/Qwen3-1.7B-GGUF) Apache 2.0 e o servidor CPU de [llama.cpp](https://github.com/ggml-org/llama.cpp/blob/master/docs/docker.md), em Docker Desktop local. Apenas textos sintéticos passaram pelo modelo. O endpoint ficou vinculado ao loopback e o contêiner foi limitado a 4 CPUs, 3 GiB de memória e uma solicitação por vez. Essa medição vale para o computador de teste.

- 5/6 saídas passaram nas verificações literais de código, links, valores e identificadores em 1,7 a 2,8 segundos por amostra.
- A revisão semântica encontrou traduções incorretas de “bancada”, “bomba” e “consigna”. A amostra PT → EN também perdeu as marcações inline em torno de dois valores. Esse modelo não passou para uso automático.
- Antes de uma nova avaliação, exigir zero alteração em instruções negativas, unidades, números, identificadores, blocos de código e endereços de links. Revisar vocabulário de automação e medir memória, latência e concorrência no ambiente alvo.

### Reproduzir o teste de qualidade

Baixar `Qwen3-1.7B-Q8_0.gguf` da fonte acima e conferir SHA-256 `061b54daade076b5d3362dac252678d17da8c68f07560be70818cace6590cb1a`. Com Docker, montar o modelo como somente leitura e publicar `127.0.0.1:18092:8080`. Usar a imagem `ghcr.io/ggml-org/llama.cpp@sha256:5b43086e3ba7ea5794d72904654f6cd41224a9b1c6ac0398f4940ee380d30f23` com os argumentos `-m /models/qwen.gguf --alias scadivo-translation-lab --reasoning off --host 0.0.0.0 --port 8080 --ctx-size 2048 --parallel 1 --threads 4`. Depois da resposta saudável de `/health`, executar `node deploy/community-homolog/check-translation.mjs` a partir de `website/scadivo`. O código de saída acusa falhas estruturais. Ler as seis traduções para a aprovação técnica.

[Documentação de localização nativa](https://meta.discourse.org/t/content-localization-manual-and-automatic-with-discourse-ai/370969) · [Configuração da versão em produção](https://github.com/discourse/discourse/blob/2c3ac01ed83af9ebeeade8b57c9353146ffb4584/plugins/discourse-ai/config/settings.yml) · [Rotina de seleção das publicações](https://github.com/discourse/discourse/blob/2c3ac01ed83af9ebeeade8b57c9353146ffb4584/plugins/discourse-ai/app/jobs/regular/detect_translate_post.rb)
