# Piloto de tradução da Comunidade SCADIVO

## Localização dos tópicos iniciais

O [pacote revisado](initial-topic-localizations.json) contém títulos e corpos em inglês e espanhol para **Regras da comunidade SCADIVO** e **Boas-vindas à comunidade SCADIVO**. O original em português continua sendo a publicação de referência. Os IDs e links do pacote pertencem a `community.scadivo.com`. Em homologação, mapear pelo título original e conferir os links no ambiente de destino.

A versão publicada, 2026.1.4 (`2c3ac01e`), dispõe da localização nativa. Habilitar **Content localization**, manter `pt_BR` como padrão e incluir `en` e `es` nos idiomas possíveis. Usar as opções de localização do título e da primeira publicação. Conferir o idioma de origem de ambos antes de cadastrar as traduções. Fazer backup das traduções existentes antes de editar e manter **AI translation** desabilitada. O seletor do tema SCADIVO já existe. Se ele atender aos visitantes e aos perfis, manter apenas esse seletor.

Antes de publicar, validar a conta de teste entrando pelo Portal com cada idioma do perfil. Conferir o título na lista, o corpo do tópico, o controle para ver o original e o retorno a português. Para visitante, conferir a seleção de idioma e os links de categorias. Para reversão, retirar as localizações novas ou restaurar as anteriores sem editar o texto original.

## Categorias públicas

O [pacote de categorias](category-localizations.json) contém os nomes e as descrições das oito categorias públicas em inglês e espanhol. As traduções são dadas à localização nativa, vinculadas ao endereço estável de cada categoria. O português permanece como referência e o pacote preserva o link das regras.

Na versão 2026.1.4, a página de categorias usa um campo de resumo distinto da descrição localizada. O plugin `scadivo-visitor-locale` entrega o resumo localizado nessa versão. Versões com suporte nativo ao resumo usam seu próprio tratamento. O ajuste não muda permissões, endereços ou tópicos.

Com a localização de conteúdo já habilitada, execute `bundle exec rails runner apply-category-localizations.rb category-localizations.json` no diretório da aplicação, apontando para as cópias dos arquivos no ambiente escolhido. O script confere os oito textos de origem e a visibilidade pública antes de gravar as localizações em uma transação. Faça uma cópia de segurança das localizações e da imagem antes da publicação. Valide em uma janela anônima com PT, EN e ES, troque no seletor e recarregue o catálogo. Na lista móvel a descrição pode ficar oculta, então confira também o catálogo no desktop.

Com as dependências de navegador instaladas, execute `node tests/browser/check-category-localizations.mjs <URL-da-Comunidade>` a partir de `website/scadivo` para repetir as verificações anônimas.

O teste local em `eafa0d12` usou os serviços nativos de criação e seleção de localizações. Os dois tópicos sintéticos passaram em português, inglês e espanhol (6/6), com links preservados. Os dados de teste foram revertidos ao fim da transação. A navegação autenticada e a publicação das traduções no domínio real ainda precisam ser verificadas.

## Tradução automática de contribuições

O motor nativo guarda traduções para reutilizar nas leituras. No commit em produção, a opção `ai_translation_backfill_limit_to_public_content` restringe a rotina de tradução de publicações a conteúdo público, incluindo a exclusão de mensagens privadas. A seleção de categorias por lista só está disponível em código posterior. Para um piloto de uma categoria, usar uma instância isolada com dados sintéticos ou validar uma versão com essa opção. Sem tradução de mensagens privadas, categorias restritas, chamados ou histórico nesse piloto.

O ensaio usou [Qwen3 1.7B Q8_0](https://huggingface.co/Qwen/Qwen3-1.7B-GGUF) Apache 2.0 e o servidor CPU de [llama.cpp](https://github.com/ggml-org/llama.cpp/blob/master/docs/docker.md), em Docker Desktop local. Apenas textos sintéticos passaram pelo modelo. O endpoint ficou vinculado ao loopback e o contêiner foi limitado a 4 CPUs, 3 GiB de memória e uma solicitação por vez. Essa medição vale para o computador de teste.

- 5/6 saídas passaram nas verificações literais de código, links, valores e identificadores em 1,7 a 2,8 segundos por amostra.
- A revisão semântica encontrou traduções incorretas de “bancada”, “bomba” e “consigna”. A amostra PT → EN também perdeu as marcações inline em torno de dois valores. Esse modelo não passou para uso automático.
- Antes de uma nova avaliação, exigir zero alteração em instruções negativas, unidades, números, identificadores, blocos de código e endereços de links. Revisar vocabulário de automação e medir memória, latência e concorrência no ambiente alvo.

### Reproduzir o teste de qualidade

Baixar `Qwen3-1.7B-Q8_0.gguf` da fonte acima e conferir SHA-256 `061b54daade076b5d3362dac252678d17da8c68f07560be70818cace6590cb1a`. Com Docker, montar o modelo como somente leitura e publicar `127.0.0.1:18092:8080`. Usar a imagem `ghcr.io/ggml-org/llama.cpp@sha256:5b43086e3ba7ea5794d72904654f6cd41224a9b1c6ac0398f4940ee380d30f23` com os argumentos `-m /models/qwen.gguf --alias scadivo-translation-lab --reasoning off --host 0.0.0.0 --port 8080 --ctx-size 2048 --parallel 1 --threads 4`. Depois da resposta saudável de `/health`, executar `node deploy/community-homolog/check-translation.mjs` a partir de `website/scadivo`. O código de saída acusa falhas estruturais. Ler as seis traduções para a aprovação técnica.

[Documentação de localização nativa](https://meta.discourse.org/t/content-localization-manual-and-automatic-with-discourse-ai/370969) · [Configuração da versão em produção](https://github.com/discourse/discourse/blob/2c3ac01ed83af9ebeeade8b57c9353146ffb4584/plugins/discourse-ai/config/settings.yml) · [Rotina de seleção das publicações](https://github.com/discourse/discourse/blob/2c3ac01ed83af9ebeeade8b57c9353146ffb4584/plugins/discourse-ai/app/jobs/regular/detect_translate_post.rb)
