'use strict';

const portalOfflineLicensing = (() => {
  const text = (pt, en, es) => tr(pt, en, es);
  const statusName = status => ({
    submitted: text('Recebida', 'Submitted', 'Recibida'),
    reviewing: text('Em análise', 'Under review', 'En revisión'),
    'needs-information': text('Aguardando informação', 'Waiting for information', 'Pendiente de información'),
    'approved-for-signing': text('Aprovada para assinatura', 'Approved for signing', 'Aprobada para firma'),
    delivered: text('Licença registrada', 'License recorded', 'Licencia registrada'),
    declined: text('Não aprovada', 'Declined', 'No aprobada')
  })[status] || status;

  function errors() {
    return {
      'invalid-offline-license-request': text('Confira o nome da aplicação, o tipo de solicitação e o código ESREQ1.', 'Check the application name, request type and ESREQ1 code.', 'Revisa el nombre de la aplicación, el tipo de solicitud y el código ESREQ1.'),
      'too-many-open-license-requests': text('Você já tem muitos pedidos de licença em andamento. Aguarde a análise dos pedidos anteriores.', 'You have too many license requests in progress. Wait for the previous requests to be reviewed.', 'Tienes demasiadas solicitudes de licencia en curso. Espera a que se revisen las anteriores.'),
      'invalid-signed-offline-license': text('Confira o ID do pedido, a assinatura, a máquina e os direitos da licença.', 'Check the request ID, signature, machine and license rights.', 'Comprueba el ID de solicitud, la firma, la máquina y los derechos.'),
      'offline-conversion-unavailable': text('Confirme o encerramento e verifique se a licença é elegível.', 'Confirm retirement and check that the license is eligible.', 'Confirma la retirada y comprueba que la licencia sea elegible.'),
      'offline-license-awaiting-expiry': text('Aguarde até a data indicada.', 'Wait until the date shown.', 'Espera hasta la fecha indicada.'),
      'invalid-license-request-status': text('Estado de solicitação inválido.', 'Invalid request status.', 'El estado de la solicitud no es válido.'),
      'invalid-license-request-transition': text('Esse pedido já avançou e não pode voltar para esse estado.', 'This request has progressed and cannot return to that status.', 'Esta solicitud ya avanzó y no puede volver a ese estado.')
    };
  }

  async function customer() {
    const requests = await api('/licenses/offline-requests');
    const options = await api('/licenses/offline-requests/conversion-options');
    const kinds = [['new', text('Nova licença offline por máquina', 'New offline machine license', 'Nueva licencia sin conexión por máquina')]];
    if (options.length) kinds.push(['convert', text('Converter licença vinculada à conta', 'Convert an account-linked license', 'Convertir una licencia vinculada a la cuenta')]);
    const kind = select('requestKind', kinds, 'new');
    const onlineLicense = select('onlineLicenseId', options.map(item => [item.id, `${item.applicationName} · ${item.publicCode}`]), options[0]?.id);
    const onlineField = field(text('Licença online elegível', 'Eligible online license', 'Licencia en línea elegible'), onlineLicense,
      text('A entrega encerra o direito online. Se já houve uma ativação, a licença offline fica disponível após 72 horas.', 'Delivery retires the online entitlement. If it was activated, the offline license becomes available after 72 hours.', 'La entrega retira el derecho en línea. Si hubo una activación, la licencia sin conexión queda disponible después de 72 horas.'));
    const application = input('applicationName', 'text', '', { minLength: 2, maxLength: 120 });
    const applicationField = field(text('Nome da aplicação', 'Application name', 'Nombre de la aplicación'), application);
    function syncKind() {
      const converting = kind.value === 'convert';
      onlineField.hidden = !converting;
      onlineLicense.disabled = !converting;
      applicationField.hidden = converting;
      application.disabled = converting;
    }
    kind.addEventListener('change', syncKind);
    syncKind();

    const requestForm = form([
      applicationField,
      field(text('Tipo de pedido', 'Request type', 'Tipo de solicitud'), kind),
      onlineField,
      field(text('Código de solicitação da máquina (ESREQ1)', 'Machine request code (ESREQ1)', 'Código de solicitud de la máquina (ESREQ1)'),
        textarea('machineRequestCode', '', { minLength: 20, maxLength: 8192, rows: 5, spellcheck: false }),
        text('Gere o código na interface de licenciamento da máquina que usará offline. O Portal o protege no armazenamento.', 'Generate this code in the licensing interface on the machine that will run offline. The Portal protects it at rest.', 'Genera el código desde la interfaz de licencias de la máquina que funcionará sin conexión. El Portal lo protege al almacenarlo.')),
      field(text('Observação (opcional)', 'Note (optional)', 'Nota (opcional)'), textarea('note', '', { required: false, maxLength: 800, rows: 3 }))
    ], text('Registrar pedido', 'Submit request', 'Enviar solicitud'), async data => {
      if (data.requestKind === 'convert')
        data.applicationName = options.find(item => item.id === data.onlineLicenseId)?.applicationName;
      else delete data.onlineLicenseId;
      await api('/licenses/offline-requests', 'POST', data);
      feedback(text('Pedido registrado. A equipe fará a análise pelo Portal.', 'Request submitted. The team will review it in the Portal.', 'Solicitud registrada. El equipo la revisará en el portal.'));
      await render();
    });

    const requestList = panel(text('Seus pedidos de licença offline', 'Your offline license requests', 'Tus solicitudes de licencia sin conexión'),
      p(text('A máquina poderá operar offline depois que a licença for emitida e instalada. Registrar o pedido não emite uma licença assinada.', 'The machine can operate offline after a license is issued and installed. Submitting a request does not issue a signed license.', 'La máquina podrá funcionar sin conexión cuando se emita e instale una licencia. Registrar la solicitud no emite una licencia firmada.')),
      requests.length ? table(
        [text('Aplicação', 'Application', 'Aplicación'), text('Solicitação', 'Request', 'Solicitud'), text('Estado', 'Status', 'Estado'), text('Enviada em', 'Submitted', 'Enviada'), text('Licença', 'License', 'Licencia')],
        requests.map(item => [item.applicationName, item.requestKind === 'convert' ? text('Conversão', 'Conversion', 'Conversión') : text('Nova', 'New', 'Nueva'), badge(statusName(item.status)), date(item.submittedAtUtc), deliveryAction(item)])
      ) : p(text('Você ainda não tem pedidos de licença offline.', 'You do not have offline license requests yet.', 'Aún no tienes solicitudes de licencia sin conexión.')));

    return h('div', {}, [
      panel(text('Solicitar licença para uso offline', 'Request an offline license', 'Solicitar una licencia para uso sin conexión'),
        p(text('A solicitação é vinculada a uma máquina e não exige conexão durante a operação. O Portal registra e analisa o pedido; não emite a licença assinada nesta etapa.', 'The request is bound to one machine and does not require a connection during operation. The Portal records and reviews it; it does not issue a signed license at this stage.', 'La solicitud se vincula a una máquina y no requiere conexión durante la operación. El Portal la registra y revisa; no emite la licencia firmada en esta etapa.')),
        requestForm),
      requestList
    ]);
  }

  function deliveryAction(item) {
    if (item.status !== 'delivered') return '—';
    if (new Date(item.availableAtUtc).getTime() > Date.now()) return date(item.availableAtUtc);
    const result = h('div');
    result.append(button(text('Ver licença assinada', 'View signed license', 'Ver licencia firmada'), async () => {
      const { signedLicense } = await api(`/licenses/offline-requests/${item.id}/signed-license`);
      result.replaceChildren(field(text('Copie para o licenciamento da máquina', 'Copy to the machine licensing screen', 'Copia en la pantalla de licencias de la máquina'),
        textarea('signedLicense', signedLicense, { readOnly: true, rows: 4, spellcheck: false })));
    }));
    return result;
  }

  async function admin() {
    const requests = await api('/admin/license-requests/offline');
    const detail = h('div');
    const statuses = [
      ['submitted', statusName('submitted')],
      ['reviewing', statusName('reviewing')],
      ['needs-information', statusName('needs-information')],
      ['approved-for-signing', statusName('approved-for-signing')],
      ['declined', statusName('declined')]
    ];
    const rows = requests.map(item => [
      `${item.userName} · ${item.displayName}`,
      item.applicationName,
      item.requestKind === 'convert' ? text('Conversão', 'Conversion', 'Conversión') : text('Nova', 'New', 'Nueva'),
      badge(statusName(item.status)),
      date(item.submittedAtUtc),
      button(text('Abrir pedido', 'Open request', 'Abrir solicitud'), async () => {
        const request = await api(`/admin/license-requests/offline/${item.id}`);
        const code = textarea('machineRequestCode', request.machineRequestCode, { readOnly: true, rows: 5, spellcheck: false, class: 'private-link' });
        const copy = button(text('Copiar código', 'Copy code', 'Copiar código'), async () => {
          try { await navigator.clipboard.writeText(request.machineRequestCode); feedback(text('Código copiado.', 'Code copied.', 'Código copiado.')); }
          catch { code.focus(); code.select(); feedback(text('Selecione e copie o código.', 'Select and copy the code.', 'Selecciona y copia el código.')); }
        });
        const content = [
          p(`${text('Conta', 'Account', 'Cuenta')}: ${request.userName} · ${request.displayName} (${request.email || '—'})`),
          p(`${text('Aplicação', 'Application', 'Aplicación')}: ${request.applicationName}`),
          request.requestKind === 'convert' ? p(`${text('Licença atual', 'Current license', 'Licencia actual')}: ${request.existingLicenseName}`) : null,
          request.requestNote ? p(`${text('Observação do cliente', 'Customer note', 'Nota del cliente')}: ${request.requestNote}`) : null,
          field(text('Código ESREQ1 protegido no armazenamento', 'ESREQ1 code protected at rest', 'Código ESREQ1 protegido al almacenarse'), code),
          copy
        ];
        if (!['delivered', 'declined'].includes(request.status)) {
          content.push(form([
            field(text('Estado da análise', 'Review status', 'Estado de revisión'), select('status', statuses, request.status)),
            p(text('A aprovação para assinatura é uma decisão administrativa; não gera nem entrega uma licença assinada.', 'Approval for signing is an administrative decision; it does not generate or deliver a signed license.', 'La aprobación para firma es una decisión administrativa; no genera ni entrega una licencia firmada.'))
          ], text('Salvar estado', 'Save status', 'Guardar estado'), async data => {
            await api(`/admin/license-requests/offline/${request.id}/status`, 'PUT', data);
            feedback(text('Estado do pedido atualizado.', 'Request status updated.', 'Estado de la solicitud actualizado.'));
            await render();
          }));
        }
        if (request.status === 'approved-for-signing') {
          const rights = request.signingRights;
          if (rights) content.push(p(`${rights.tier} · ${text('Interativas', 'Interactive', 'Interactivas')} ${rights.interactiveSeats} · ${text('Visualização', 'View only', 'Solo lectura')} ${rights.viewOnlySeats} · HA ${rights.haRuntime ? '✓' : '—'} · Intelligence ${rights.assetProcessIntelligence ? '✓' : '—'}`));
          const confirmRetirement = input('retireConfirmed', 'checkbox', '', { required: true });
          const converting = request.requestKind === 'convert';
          content.push(form([
            field(text('ID da licença para o gerador', 'License ID for the generator', 'ID de licencia para el generador'), input('generatorLicenseId', 'text', request.id, { readOnly: true })),
            field(text('Código assinado (ESLIC2 ou ESLIC3)', 'Signed code (ESLIC2 or ESLIC3)', 'Código firmado (ESLIC2 o ESLIC3)'),
              textarea('signedLicense', '', { maxLength: 8192, rows: 5, spellcheck: false })),
            ...(converting ? [field(text('Confirmo o encerramento definitivo da licença online selecionada', 'I confirm permanent retirement of the selected online license', 'Confirmo la retirada definitiva de la licencia en línea seleccionada'), confirmRetirement)] : [])
          ], text('Registrar licença para entrega', 'Record license for delivery', 'Registrar licencia para entrega'), async data => {
            await api(`/admin/license-requests/offline/${request.id}/signed-license`, 'PUT', { signedLicense: data.signedLicense, retireConfirmed: converting && confirmRetirement.checked });
            feedback(text('Licença registrada para entrega.', 'License recorded for delivery.', 'Licencia registrada para entrega.'));
            await render();
          }));
        }
        detail.replaceChildren(panel(text('Pedido de licença offline', 'Offline license request', 'Solicitud de licencia sin conexión'), ...content));
      })
    ]);
    return h('div', {}, [
      panel(text('Pedidos de licença offline por máquina', 'Offline machine license requests', 'Solicitudes de licencia sin conexión por máquina'),
        p(text('Os códigos ficam protegidos no armazenamento e são revelados somente nesta visualização administrativa, com auditoria. Esta etapa não assina licenças.', 'Codes are protected at rest and revealed only in this audited administrative view. This stage does not sign licenses.', 'Los códigos se protegen al almacenarse y solo se revelan en esta vista administrativa auditada. Esta etapa no firma licencias.')),
        requests.length ? table(
          [text('Usuário', 'User', 'Usuario'), text('Aplicação', 'Application', 'Aplicación'), text('Tipo', 'Type', 'Tipo'), text('Estado', 'Status', 'Estado'), text('Data', 'Date', 'Fecha'), text('Ação', 'Action', 'Acción')],
          rows
        ) : p(text('Nenhum pedido recebido.', 'No requests received.', 'No se han recibido solicitudes.'))),
      detail
    ]);
  }

  return { customer, admin, errors };
})();
