# Licenciamento do produto — próxima frente após Help

## Estado e origem da ordem

**QUEUED_AFTER_HELP / NOT_IMPLEMENTED.** Em 25/10/2026 o chat responsável pelo Portal reenviou o handoff, comunicando instrução do Product Owner: concluir a frente atual de Help e, logo depois, integrar o licenciamento no produto sob a #301. O recebimento e a prioridade ficam registrados no GitHub vivo. Não significa que Help foi aceito, nem autoriza trocar de frente agora, emitir licenças ou habilitar assinatura.

Contrato autoritativo: [#301, comentário 6392078669](https://github.com/brunolrogerio-collab/EliteSCADA/issues/301#issuecomment-6392078669). A prioridade foi publicada pelo Portal no [comentário 6402669195](https://github.com/brunolrogerio-collab/EliteSCADA/issues/301#issuecomment-6402669195), conferido no recebimento. Portal: [#741](https://github.com/brunolrogerio-collab/EliteSCADA/issues/741); [retomada única #756](https://github.com/brunolrogerio-collab/EliteSCADA/issues/756#issuecomment-6366027429). Esta referência complementa o contrato, sem substituí-lo.

Sequência registrada: **concluir Help #424/#425 → licenciamento do produto #301 → demais melhorias operacionais #779–#782**, preservando a ordem relativa dessas melhorias. A auditoria de interface #357 e as capturas reais finais #425 continuam ligadas à convergência do Help/manual; um CI verde ou um conjunto de tópicos não declara esse trabalho concluído.

## Estado conferido no recebimento

Metadados e checks do GitHub foram conferidos; ainda não se declara auditoria integral da implementação/API nem compatibilidade ponta a ponta com o produto.

| PR | Escopo | Base → head conferidos | Estado |
| --- | --- | --- | --- |
| [#778](https://github.com/brunolrogerio-collab/EliteSCADA/pull/778) | API online | `main@3b520aff3c249d0c7e0b8c2957344226de911ed7` → `3f825c9750eed841d309004af452cb5bff3d0db5` | aberto/draft; quatro checks aprovados |
| [#783](https://github.com/brunolrogerio-collab/EliteSCADA/pull/783) | UI licenças/organizações/aplicações/ativações | #778 head → `e5debb2b6a7962cb5cfb470ec1fff4f1b60d5c5c` | aberto/draft; Portal aprovado |
| [#784](https://github.com/brunolrogerio-collab/EliteSCADA/pull/784) | Downloads/Admin delegado e leitura de ativações | #783 head → `2a2d1e7a33dc9ab8364c7fda53ee44073a8f8b5b` | aberto/draft; Portal aprovado; entrega informa run 40229178674 |
| [#777](https://github.com/brunolrogerio-collab/EliteSCADA/pull/777) | Intake offline ESREQ1 | `main` → `2e2cf0c6ad3fbb107c802dea87f9af4d8b164f63` | separado, aberto/draft; não é API de emissão online |

Produto: `wave15/corrections-integration@d41c2f84ba179f5eae01dc1df96c9c1a28830dc7`. Help: PR draft #772 no head `b4b4bb4f9d4fbd1d01b9e06c239dd784d4f67045`, com [T1 40204142092 aprovado](https://github.com/brunolrogerio-collab/EliteSCADA/actions/runs/40204142092). Esses SHAs são snapshots; revalidar branches/heads, contratos e trabalho concorrente antes de implementar.

## Execução do produto após o Help

1. **Revisão de compatibilidade:** ler o contrato #301 e código/documentação reais do #778 (`website/scadivo/portal/LICENSING_API.md`, operações/endpoints e signer), confrontar com `src/Scada.Core/Product/Licensing/LicenseContracts.cs` e o lifecycle existente. Trabalhar por PR(s) próprios sobre a branch Wave 15 apropriada; não misturar código de licença ao PR de Help nem integrar a cadeia do Portal como bloco de produto.
2. **Verificador online:** adicionar ESLEASE1 com prefixo/schema/propósito/KeyId próprios, preservando ESLIC1/schema 1, ESLIC2/schema 2 e ESLIC3/schema 3. Incorporar somente a chave pública `scadivo-online-lease-2026-01`, RSA3072/PSS-SHA256; fingerprint SPKI SHA256 `26a33c58eeeb1329e8901b2722776545be5a8a266016cc44295bf115960d59aa`. Validar máquina e IDs esperados, datas, intervalo positivo/TTL máximo 72 h e proteção contra retrocesso de relógio. Sem chave privada em código, pacote, imagem ou logs. Preservar a Security Authority e separar contas do Portal das identidades do produto.
3. **Ativação/renovação:** HTTPS com usuário/e-mail + senha + License ID, confirmação da aplicação resolvida pela licença, sem selecionar outra silenciosamente. O produto inicia renovação a cada 48 h e repete antes do vencimento, com idempotência para resposta perdida, rotação/replay/revogação/troca de senha e armazenamento protegido da credencial fora do projeto/pacote. Windows, Linux e Docker precisam de caminhos suportados; conferir identidade estável em Docker e ativação/lease próprios por nó HA. O snapshot atual não prova esses mecanismos entregues no produto.
4. **Lifecycle e Demo:** integrar expiração no coordenador de licença existente ou mecanismo equivalente, reavaliando Run, sessões, HA e Intelligence. Expiração online entra explicitamente em Demo sem apagar dados; Demo sem timer de Run, 200 TAGs e pools compartilhados Web/EliteGO de 2 Interactive + 2 View Only, sem HA/Intelligence. Acima de 200 definições não descartar TAGs parcialmente: controlar/recusar Run com motivo claro. Licença offline instalada inválida permanece Invalid/bloqueante. A base conferida ainda contém `DemoMaxContinuousRun = 300 minutos`; isso é pendência de implementação, não licença Demo ilimitada já entregue.
5. **Aba Licença:** modos online/offline com campos condicionais; License ID, termo/modalidade, datas/renovação, uso/limites vindos do backend e instante do snapshot. Contar definições de TAG e leases lógicos por classe, somando Web/EliteGO; não inventar zero quando indisponível nem contar sockets como sessões. HA/Intelligence mostram capacidade e estado ativo. Não expor senha, credencial de renovação ou material privado.
6. **Aceite conjunto:** testes de prefixo/schema/chave/propósito cruzados, assinatura inválida, IDs/máquina, datas/TTL/relógio, idempotência, replay, senha/revogação, várias licenças por conta, identidade Docker, nós HA e transições Run/sessões/Intelligence. Validar APIs/contadores e UI nos modos relevantes com Portal/produto em ambiente isolado. Publicar PR, base/head exatos, evidência local, CI e limites na #301. Atualizar Help/manual e seus marcadores após o comportamento aceito; não documentar o futuro como disponível hoje.

## Dependências e limites preservados

A emissão do Portal permanece desativada até verificador/renovação/lifecycle do produto e operação da chave estarem integrados e validados conjuntamente. A privada não foi provisionada no servidor; este handoff não a lê, move nem instala. Reutilizar a operação protegida existente e o contrato #301 para rotação/revogação; o TTL não substitui resposta a comprometimento da chave. Quantidade comercial de ativações e operação offline continuam decisões pendentes registradas no Portal. Não criar um novo bloqueio comercial por interpretação deste documento.

Nenhum merge/deploy de produção, emissão, provisão de chave ou alteração da Preview/DEMO é parte deste recebimento. O Portal continua seu próprio escopo #741; o registro de produto na #756 não substitui o próximo passo do chat do Portal.
