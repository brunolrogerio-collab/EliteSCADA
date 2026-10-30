# Backup e recuperação

## Backup e recuperação: Authority, aplicação e dados

Separe os arquivos necessários, valide antes de aplicar e saiba o que cada etapa efetivamente restaura.

### Escolha o que precisa recuperar

Antes de restaurar, identifique o que perdeu e quais cópias tem. A aplicação .escadapkg leva a engenharia: TAGs, telas, Scripts e demais objetos portáteis. A Security Authority mantém usuários, senhas e política de acesso em outro backup protegido. Licença é um arquivo separado.

Banco de dados, histórico Historian e chaves/material protegido dos drivers seguem procedimentos próprios. O pacote da aplicação não é uma cópia completa da instalação e não contém usuários, hashes de senha ou credenciais de login. Ter apenas .escadapkg não garante recuperar acesso, histórico ou chave de driver.

### Preparar e conferir os backups

Exporte a aplicação pelo fluxo disponível e confira projeto, revisão e destino do arquivo. Resolva alterações não salvas antes de tratar a cópia como recuperável. Aguarde o download terminar.

Em Trocar a aplicação desta instalação existem Backup da Application e Backup da Authority antes do detach, a desvinculação. Você pode exportar e voltar sem executar a troca. Não faça detach só para testar backup.

Na Authority, Nova senha do backup protege o arquivo exportado; não é senha de login. Guarde-a por meio protegido separado do arquivo. Prepare também cópias e recuperação do banco/Historian e chaves de drivers. Este fluxo não define retenção nem inclui arquivos do Historian. Para arquivar e restaurar amostras brutas, use a seção Arquivar e restaurar amostras descrita em [Captura e consulta do Historian](07-HISTORIAN-CAPTURA-CONSULTA.md); esse arquivo é administrado separadamente pela API.

### Recuperar Authority e aplicação em uma instalação neutra

1. Confirme instalação neutra elegível e servidor de destino correto. Não faça detach só para abrir este fluxo.
2. Na configuração inicial, escolha Restaurar backup e selecione aplicação, Authority, senha do backup e licença opcional.
3. Use Validar backups. Se houver recusa, corrija arquivo, compatibilidade ou credencial. A extensão do arquivo não substitui a validação.
4. Com os arquivos válidos, use Restaurar Authority.
5. Entre com um Administrador restaurado, habilitado e com permissões efetivas exigidas para o pacote. O nome developer sozinho não basta.
6. Continue a recuperação. O servidor valida novamente a aplicação usando a Authority restaurada.
7. Quando permitido, use Importar aplicação e aguarde o resultado completo. Restaurar Authority e recuperar a aplicação são etapas distintas; não há promessa de uma transação única entre todos os arquivos.

### Recuperar apenas a aplicação com Authority existente

Se a instalação não tem projeto persistido mas já tem Authority, entre com um Administrador compatível e use Importar aplicação. Selecione o .escadapkg, use Validar aplicação, leia o resultado e só então importe. O fluxo exige compatibilidade do pacote, catálogo de projeto/estado de destino apropriados e autorização; não é uma função genérica para sobrescrever qualquer aplicação ativa. Para modificar uma aplicação já instalada, use os fluxos normais de engenharia/portabilidade correspondentes ou o procedimento específico de troca. Não apague usuários/banco para fazer a opção aparecer.

### Entenda os campos dos arquivos

Na entrada neutra elegível, Restaurar backup permite preparar Authority e aplicação. Se já existe Authority e falta apenas a aplicação, use Importar aplicação com a Authority atual; não force uma nova configuração inicial para contornar o estado existente.

| Campo | Para que serve e como preencher | Exemplo | Limites |
| --- | --- | --- | --- |
| Pacote da aplicação (.escadapkg) | Escolha o arquivo exportado da aplicação que pretende recuperar. Confira identidade e compatibilidade na validação. | Arquivo da aplicação exportada | Selecionar não importa nem ativa; extensão/nome sozinhos não provam validade. |
| Backup da Authority (.json) | No bootstrap de recuperação, selecione o backup protegido da Authority. | Backup separado da Authority | Não substituir por um JSON editado nem pelo pacote da aplicação. |
| Senha do backup da Authority | Use a senha escolhida ao exportar esse backup. Ela abre o arquivo protegido, não autentica um usuário. | Senha guardada separadamente | De 12 a 1024 caracteres. Não existe senha de exemplo ou recuperação por edição do arquivo. Erro exige conferir origem/credencial. |
| Licença (opcional) | Selecione a licença se também quiser tentar instalá-la após a recuperação da aplicação. | Arquivo de licença compatível | Opcional; falha de licença pode deixar a aplicação recuperada sem a licença desejada. |
| Validar backups | Confere o backup da Authority e o pacote antes de permitir restaurar a Authority. | Backups válidos | Preview não restaura. Troca de arquivo/senha exige validação novamente. |
| Restaurar Authority | Aplica a Authority validada quando o bootstrap é permitido. Depois entre com um Administrador restaurado. | Authority restaurada | Restauração de acesso não equivale a aplicação já importada; sessões antigas não devem ser reutilizadas. |
| Validar aplicação | Confere o pacote contra o estado/Authority atuais, inclusive após restaurar a Authority. | Pacote válido e compatível | Uma validação anterior ao restore da Authority não substitui a nova validação. |
| Importar aplicação | Executa a recuperação aceita pelo servidor: substituição do Working, revisão raiz persistida, publicação e ativação pelo lifecycle. | Application importada, publicada e ativada | O resultado completo exige todas as etapas; um estágio intermediário não é sucesso integral. |
| Continuar recuperação | Retoma após login com os mesmos arquivos conservados pelo navegador. | Arquivos já selecionados | Se o navegador perdeu os arquivos, selecione novamente e valide; nomes/tamanhos exibidos não substituem a verificação do servidor. |
| Continuar sem licença | Prossegue quando a aplicação foi recuperada, mas a licença opcional falhou. | Aviso de licença separado | Não comprova capacidade/licença para operar; confira Licenciamento e sessão Runtime depois. |

### Interrupção, falha parcial e licença

Restaurar a Authority não significa que a aplicação já foi recuperada. Se o navegador conserva os arquivos selecionados, a continuação mantém essa seleção e impede trocá-la silenciosamente. Se os perdeu, selecione novamente os arquivos pedidos e faça nova prévia antes de Apply. A memória da seleção não é backup dos arquivos.

Se aparecer estágio ou erro intermediário, registre o resultado e confira o estado salvo antes de repetir. O fluxo passa por revisão, publicação e ativação; uma etapa concluída não deve ser presumida desfeita.

Se a aplicação foi recuperada e só a licença opcional falhou, leia o aviso. Use Continuar sem licença quando apropriado e trate a licença depois, sem repetir a restauração inteira por esse aviso.

### Confirmar o resultado e os limites

Confira autenticação com a Authority esperada, projeto/revisão ativos, telas, scripts e fontes no destino. No Runtime, confirme classe de sessão, valores, qualidade e horários. Banco/Historian são recuperação separada pelo procedimento nativo suportado; não invente histórico a partir de TAG atual. Material protegido de drivers pode exigir preparação no host. Arquivo corrompido, senha errada, usuário restaurado desabilitado, referência de Authority incompatível ou falta de permissões devem ser resolvidos na origem correta. Nunca edite stores, tokens ou arquivos assinados para forçar aceitação. Faça o ensaio de recuperação em ambiente controlado antes de depender da cópia para uma operação real.

[Abrir este guia na Ajuda instalada](/help?topic=recovery.backup-system-recovery)
