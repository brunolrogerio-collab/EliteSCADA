# Backup e recuperação

## Backup e recuperação: Authority, aplicação e dados

Separe os arquivos necessários, valide antes de aplicar e saiba o que cada etapa efetivamente restaura.

### Escolha o que precisa recuperar

Aplicação é a engenharia: TAGs, telas, scripts e demais objetos portáteis do pacote .escadapkg. Security Authority mantém usuários, credenciais e política de acesso em um backup protegido separado. Licença é outro artefato. Banco de dados, Historian e material protegido de drivers pertencem aos seus procedimentos próprios. Um arquivo de aplicação não é uma imagem completa da instalação e não contém usuários/password hashes/senhas de login. Não conclua que ter o .escadapkg basta para recuperar acessos, histórico ou as chaves de um driver.

### Preparar e conferir os backups

Exporte a aplicação pelo fluxo suportado e confira projeto/revisão e local de destino. Alterações não salvas precisam ser tratadas antes de considerar uma cópia como ponto recuperável. O fluxo Trocar a aplicação desta instalação oferece Backup da Application e Backup da Authority antes do detach: você pode exportar a cópia necessária e voltar, sem executar a troca. Na Authority, Nova senha do backup protege o arquivo exportado; não é a senha de login. Guarde essa senha por um meio protegido separado do arquivo e confirme que o download terminou. Não avance para o detach só para testar um backup. Mantenha cópias e plano de recuperação próprios do banco/Historian e das chaves protegidas; este guia não cria política de retenção/arquivamento nem promete incluí-los no pacote.

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

### Recuperar Authority e aplicação em uma instalação neutra

1. Confirme que a instalação está no estado neutro elegível e que você está no servidor de destino correto; não faça detach só para abrir este fluxo.
2. Na configuração inicial, escolha Restaurar backup. Selecione aplicação, Authority, senha do backup e licença opcional.
3. Escolha Validar backups. Se houver rejeição, pare e corrija arquivo/compatibilidade/credencial; nenhum aceite de extensão substitui a validação.
4. Com os arquivos válidos, escolha Restaurar Authority.
5. Entre com um Administrador restaurado. A conta precisa estar habilitada e ter as permissões efetivas de engenharia/administração exigidas para o pacote; o nome developer sozinho não basta.
6. Continue a recuperação; o servidor valida novamente a aplicação com a Authority restaurada.
7. Quando a validação permitir, escolha Importar aplicação e aguarde o resultado completo. A restauração da Authority e a recuperação da aplicação são etapas distintas, não uma promessa de transação única entre todos os arquivos.

### Recuperar apenas a aplicação com Authority existente

Se a instalação não tem projeto persistido mas já tem Authority, entre com um Administrador compatível e use Importar aplicação. Selecione o .escadapkg, use Validar aplicação, leia o resultado e só então importe. O fluxo exige compatibilidade do pacote, catálogo de projeto/estado de destino apropriados e autorização; não é uma função genérica para sobrescrever qualquer aplicação ativa. Para modificar uma aplicação já instalada, use os fluxos normais de engenharia/portabilidade correspondentes ou o procedimento específico de troca. Não apague usuários/banco para fazer a opção aparecer.

### Interrupção, falha parcial e licença

Após restaurar a Authority, a aplicação pode ainda não estar recuperada. Se os arquivos continuam em memória do navegador, a continuação conserva a seleção e impede trocá-la silenciosamente. Se a página perdeu os arquivos, escolha novamente os artefatos solicitados; uma nova prévia é obrigatória antes de Apply. Não confunda a memória da seleção com backup dos arquivos. Se o servidor retornar estágio/erro intermediário, registre o resultado e confira o estado persistido antes de repetir: o fluxo envolve revisão, publicação e ativação, e uma etapa concluída não deve ser presumida desfeita. Se a aplicação foi recuperada e a licença opcional falhou, leia o aviso e use Continuar sem licença quando apropriado; trate a licença depois, sem repetir o restore inteiro só por esse aviso.

### Confirmar o resultado e os limites

Confira autenticação com a Authority esperada, projeto/revisão ativos, telas, scripts e fontes no destino. No Runtime, confirme classe de sessão, valores, qualidade e horários. Banco/Historian são recuperação separada pelo procedimento nativo suportado; não invente histórico a partir de TAG atual. Material protegido de drivers pode exigir preparação no host. Arquivo corrompido, senha errada, usuário restaurado desabilitado, referência de Authority incompatível ou falta de permissões devem ser resolvidos na origem correta. Nunca edite stores, tokens ou arquivos assinados para forçar aceitação. Faça o ensaio de recuperação em ambiente controlado antes de depender da cópia para uma operação real.

[Abrir este guia na Ajuda instalada](/help?topic=recovery.backup-system-recovery)
