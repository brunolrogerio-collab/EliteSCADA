# Trocar a aplicação da instalação

## Trocar a aplicação: conferir, preservar e desvincular

Prepare as cópias e entenda o que muda antes de retirar a aplicação atual desta instalação.

### O que esta operação faz

Use Engineering, Instalação quando precisar preparar este servidor para outra aplicação. A operação retira o vínculo da aplicação atual, encerra a validade dos acessos antigos e volta à configuração inicial. A aplicação deixa de operar nesse servidor. O produto continua instalado e o Historian não é apagado por esse fluxo. Para apenas editar uma tela ou colocar outra revisão do mesmo projeto em uso, siga o lifecycle do projeto; não faça esta troca.

### Preparar as cópias antes de decidir

1. Confira Application atual, Working e Active para ter certeza de que está na instalação correta.
2. Veja se há alterações não salvas. Salve uma revisão se precisar preservar esse trabalho; salvar ainda não coloca as alterações em execução.
3. Use Exportar Application e confirme o download do arquivo .escadapkg. Ele contém a engenharia da aplicação.
4. Para levar também usuários e permissões, use Exportar Authority. A Authority é a parte que guarda esses acessos; seu backup é separado e usa a Nova senha do backup. Guarde a senha separadamente do arquivo.
5. Confirme com o responsável as cópias de banco, histórico e chaves de drivers. A tela não inclui tudo isso no pacote da aplicação.

### Confirmar a decisão e aguardar

1. Com as cópias conferidas, abra Desvincular aplicação desta instalação.
2. Leia as confirmações sobre aplicação, usuários/sessões, histórico e trabalho não salvo. Confirme somente o que compreendeu e decidiu fazer.
3. Escolha o que fazer com a licença da máquina: Manter conserva a atual; Remover a retira; Substituir pede um código novo válido. Essa escolha é separada da aplicação.
4. Se houver bloqueios, resolva-os e atualize o estado.
5. Clique em Desvincular e voltar ao neutro uma vez e aguarde. O servidor confere a situação novamente antes de aplicar. Com sucesso, a página volta à configuração inicial. Você ainda precisará criar, importar ou recuperar a próxima aplicação.

### Consultar o resumo e os campos de cópia

Use esta tabela quando precisar esclarecer um dos campos da preparação. Exportar uma cópia não exige executar a troca.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Application atual | Identifica o projeto que deixará de estar vinculado a esta instalação. | Projeto atual | Não é escolha de um servidor remoto ou de um novo projeto. |
| Working | Mostra revisão e alterações ainda não salvas. | Alterações não salvas | Salvar não publica nem ativa; exporte a cópia necessária. |
| Published e Active | Compare revisão publicada e revisão executada para entender o que precisa preservar/parar. | Revisões exibidas | Não presuma que o Working contém exatamente o Active. |
| Security Authority | Identidades e política que serão desvinculadas; sessões antigas perderão validade. | Authority presente | Backup separado; não se recupera senha de login pelo .escadapkg. |
| Historian | Indica que o histórico/banco é preservado por este fluxo. | Preservado | Não é exportação de dados nem promessa de compatibilidade/ligação automática da próxima aplicação. |
| Proteção de engenharia | Mostra a proteção atual e eventuais condições que precisam ser resolvidas pelo fluxo autorizado. | Estado da proteção | Não remova proteção/credenciais para contornar bloqueios. |
| Exportar Application | Produz a cópia portátil da engenharia pelo fluxo suportado. | Download .escadapkg | Confira arquivo/revisão/destino; download cancelado não é cópia concluída. |
| Nova senha do backup / Exportar Authority | Exporta Authority protegida, com senha própria. | Backup separado concluído | Senha do backup de 12 a 1024 caracteres; não publicar arquivo/senha nem confundir com login. |

### Consultar confirmações e licença

As confirmações existem para você reconhecer o que será afetado. Marcar uma caixa não cria uma cópia. Se uma mensagem não estiver clara, volte à preparação e confira antes de continuar.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Decisão sobre backups | Confirma exportação feita nesta revisão ou escolha explícita de continuar sem uma das cópias. | Backups exportados | Continuar sem exportar assume a falta daquela cópia; não é backup automático. |
| Perda de Working não salvo | Exigido quando existem alterações não salvas e você decide prosseguir. | Reconhecimento da consequência | Não salva as alterações nem garante poder recuperá-las. |
| Desvincular Application | Reconhece que a aplicação atual deixa de operar vinculada à instalação. | Consequência lida | Não é um simples fechamento da interface. |
| Desvincular Authority e sessões | Reconhece a retirada do vínculo e a invalidação da autorização das sessões anteriores. | Consequência lida | Usuários/policy novos ou restaurados devem seguir bootstrap/recuperação próprios. |
| Preservação do Historian/banco | Reconhece a separação dos dados históricos e a ausência de limpeza silenciosa. | Dados preservados | Não executa migração, descarte ou restauração de histórico. |
| Licença da máquina | Manter preserva a licença atual; Remover retira deliberadamente; Substituir exige uma candidata válida. | Manter licença | Licença pertence à instalação, não é arquivo embutido na aplicação. Confira o resultado após a ação. |
| Código da licença substituta | Preencha somente quando selecionar Substituir licença. A candidata é validada antes do detach. | Código obtido pelo fluxo autorizado | Não use chave criptográfica; candidata inválida deve ser corrigida antes de prosseguir. |
| Bloqueios e diagnóstico | Leia os motivos de recusa e a etapa informada; revalide o estado depois de corrigir a causa. | Bloqueio exibido | Botão indisponível não deve ser contornado por editar store ou repetir requisições. |
| Desvincular e voltar ao neutro | Envia a decisão ao servidor, que faz prévia/revalidação antes de aplicar o detach. | Instalação neutra | A operação pode ser recusada por mudança de estado entre as etapas. |
| Cancelar / Voltar | Antes da execução, permite retornar à preparação sem confirmar o detach. | Revisar cópias | Não promete desfazer uma operação já recebida/concluída pelo servidor. |

### Se a conexão cair ou surgir um erro

Não repita a operação imediatamente. Reabra o endereço correto e confira se ainda está a aplicação anterior ou se a instalação voltou à configuração inicial. Uma queda de conexão não diz, por si só, se a troca terminou. Anote mensagem, etapa e horário e peça a conferência do estado se houver dúvida. Cancelar ou voltar antes da confirmação não desfaz uma operação que o servidor já concluiu. Uma licença substituta inválida precisa ser corrigida; não remova a válida como tentativa de resolver o erro. Para recuperar cópias, use Backup e recuperação.

[Abrir este guia na Ajuda instalada](/help?topic=installation.switch-workflow)
