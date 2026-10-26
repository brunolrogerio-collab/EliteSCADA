# Trocar a aplicação da instalação

## Trocar a aplicação: conferir, preservar e desvincular

Entenda a decisão de detach e cada confirmação antes de devolver esta instalação ao estado neutro.

### Quando essa troca é apropriada

Abra Engineering → Instalação para preparar esta mesma instalação para outra aplicação. Não é a rotina de editar uma tela ou ativar a próxima revisão do mesmo projeto. O detach desvincula a aplicação e a Authority, invalida sessões antigas e interrompe seus efeitos de Runtime. Não desinstala o produto nem apaga silenciosamente o Historian. A escolha de licença é explícita. Faça a operação somente no destino e na parada autorizados; este texto não executa a troca.

### Conferir o estado e preparar cópias

1. Confira Application atual, Working, Published, Active, Authority, licença, Historian e proteção de engenharia. Atualize se a situação mudou.
2. Se há alterações Working não salvas, salve uma revisão quando necessário antes de considerar a cópia recuperável. Working salvo mas não publicado é outra condição; compare os números.
3. Use Exportar Application para obter o .escadapkg e confirme o download.
4. Para preservar usuários/credenciais/policy, informe Nova senha do backup e use Exportar Authority. Guarde a senha separadamente do arquivo. O pacote da Application não carrega esses acessos.
5. Trate separadamente backup de banco/Historian e material protegido de host; preservar no detach não equivale a ter uma cópia de recuperação.

### Resumo e preservação

Estes dados identificam o alvo atual; leia-os antes de abrir a confirmação.

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

### Ler as confirmações e a licença

Abra Desvincular aplicação desta instalação. Verifique cada item de forma deliberada; marcar uma caixa não faz backup nem valida um novo ambiente.

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

### Executar e confirmar o resultado

Depois de conferir cópias, consequências e escolha de licença, use Desvincular e voltar ao neutro uma vez e aguarde. A interface primeiro valida o estado e depois pede o detach; o servidor revalida a condição atual. Com sucesso, a página retorna à entrada neutra. Confirme o novo estado antes de iniciar criação/importação/recuperação. A próxima aplicação e a Authority precisam ser preparadas pelo fluxo normal; as sessões antigas não autorizam o novo vínculo. Este guia não introduz outro lock, transporte ou contrato de detach.

### Falha, interrupção ou estado incerto

Se houver bloqueio, leia a causa e atualize após resolvê-la. Licença substituta rejeitada não deve ser contornada removendo a válida por tentativa. Se perder a conexão ou fechar a página durante a ação, não suponha sucesso, cancelamento ou rollback: reabra o endereço correto e confira o estado real. Anote etapa, mensagem e horário. Não faça uma segunda troca até reconciliar o resultado. Para recuperar aplicação/Authority de cópias, use o guia de recuperação; para banco/Historian, o procedimento nativo separado. Nenhum desses procedimentos deve ser substituído por edição manual do banco.

[Abrir este guia na Ajuda instalada](/help?topic=installation.switch-workflow)
