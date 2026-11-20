'use strict';

const portalSupplement = (() => {
  const publicPath = '/privacy/supplement';
  const adminPath = '/admin/privacy/supplement';
  const filename = 'scadivo-copia-complementar';
  const jsonType = 'application/json';
  const zipType = 'application/zip';
  const contact = () => h('a', { href: '/contato?area=contato&subject=privacy&lang=' + state.lang },
    tr('Contato → Privacidade', 'Contact → Privacy', 'Contacto → Privacidad'));

  function claimant({ operation, token }) {
    const verification = operation === 'verify';
    const title = verification
      ? tr('Confirmar pedido de privacidade', 'Confirm privacy request', 'Confirmar solicitud de privacidad')
      : tr('Baixar cópia complementar', 'Download supplemental copy', 'Descargar copia complementaria');
    const contents = panel(title, p(verification
      ? tr('Confirme o pedido feito ao atendimento. Use este mesmo navegador quando o atendimento enviar o link para baixar sua cópia.',
        'Confirm the request you sent to support. Use this same browser when support sends the download link.',
        'Confirma la solicitud que enviaste a soporte. Usa este mismo navegador cuando soporte envíe el enlace de descarga.')
      : tr('Abra este link no navegador em que você confirmou o pedido. A cópia pode ser baixada uma vez.',
        'Use the browser where you confirmed the request. The copy can be downloaded once.',
        'Usa el navegador en el que confirmaste la solicitud. La copia solo se puede descargar una vez.')));
    const action = form([], title, async () => {
      if (verification) await api(publicPath + '/verify', 'POST', { token });
      else {
        const copy = await api(publicPath + '/download', 'POST', { token }, true);
        saveDownloadedFile(copy, filename + (copy.type === zipType ? '.zip' : '.json'));
      }
      action.remove();
      contents.append(notice(verification
        ? tr('Pedido confirmado. Aguarde a liberação da cópia pelo atendimento.', 'Request confirmed. Support will contact you when your copy is ready.', 'Solicitud confirmada. Soporte te avisará cuando la copia esté lista.')
        : tr('Download iniciado.', 'Download started.', 'Descarga iniciada.')));
    });
    if (state.session?.supplementalCopyEnabled) contents.append(action);
    else contents.append(notice(errorText('privacy-link-unavailable')));
    contents.append(p([tr('Precisa de ajuda? ', 'Need help? ', '¿Necesitas ayuda? '), contact()]));
    app.replaceChildren(h('div', { class: 'invite-layout' }, contents));
  }

  function shareLink(caption, url) {
    return field(caption, h('input', { type: 'text', value: url, readOnly: true, onfocus: event => event.target.select() }));
  }

  async function administration() {
    const evidence = {
      'account-email': tr('E-mail já confirmado no Portal', 'Email already verified in the Portal', 'Correo ya verificado en el Portal'),
      'original-thread': tr('Endereço da conversa original', 'Address from the original correspondence', 'Dirección de la conversación original'),
      'independent-channel': tr('Canal independente previamente verificado', 'Previously verified independent channel', 'Canal independiente previamente verificado')
    };
    const status = {
      pending: tr('Aguardando confirmação', 'Awaiting confirmation', 'Pendiente de confirmación'),
      verified: tr('Confirmado', 'Confirmed', 'Confirmado'),
      released: tr('Disponível para download', 'Available for download', 'Disponible para descarga'),
      downloaded: tr('Download iniciado', 'Download started', 'Descarga iniciada'),
      revoked: tr('Revogado', 'Revoked', 'Revocado')
    };
    const rows = h('div');
    const result = h('div', { 'aria-live': 'polite' });
    const response = url => result.replaceChildren(shareLink(
      tr('Copie o link e envie pelo canal verificado', 'Copy the link and send it via the verified channel', 'Copia el enlace y envíalo por el canal verificado'), url));
    async function refresh() {
      const copies = await api(adminPath);
      rows.replaceChildren(...copies.map(copy => {
        const actions = [];
        if (copy.status === 'verified') {
          actions.push(form([
            field(tr('Arquivo da cópia complementar', 'Supplemental copy file', 'Archivo de la copia complementaria'),
              h('input', { type: 'file', name: 'archive', accept: '.zip,.json,application/zip,application/json', required: true }),
              tr('Para incluir anexos, envie um ZIP de até 32 MB com as mensagens revisadas e seus anexos. Para dados sem anexos, também é possível enviar JSON de até 4 MB.',
                'For attachments, upload a ZIP up to 32 MB with the reviewed messages and their attachments. Data without attachments can also be uploaded as JSON up to 4 MB.',
                'Para incluir adjuntos, envía un ZIP de hasta 32 MB con los mensajes revisados y sus adjuntos. Los datos sin adjuntos también se pueden enviar como JSON de hasta 4 MB.')),
            field(tr('Revisei a cópia e confirmei que ela contém apenas dados deste titular.',
              'I reviewed the copy and confirmed that it contains only this person’s data.',
              'Revisé la copia y confirmé que contiene solo los datos de esta persona.'),
            h('input', { type: 'checkbox', name: 'reviewed', required: true }))
          ], tr('Liberar cópia', 'Release copy', 'Habilitar copia'), async (data, node) => {
            const file = node.elements.archive.files[0];
            if (!data.reviewed) throw new Error(errorText('privacy-check-required'));
            const extension = file?.name.toLowerCase().split('.').pop();
            if (!file?.size || !['zip', 'json'].includes(extension)) throw new Error(errorText('privacy-copy-invalid-file'));
            const zip = extension === 'zip';
            if (file.size > (zip ? 32 : 4) * 1024 * 1024) throw new Error(errorText('privacy-copy-file-too-large'));
            const upload = await fetch('/portal/api' + adminPath + '/upload?id=' + encodeURIComponent(copy.id), {
              method: 'POST', credentials: 'same-origin', body: file,
              headers: { 'Content-Type': zip ? zipType : jsonType, 'X-Privacy-Reviewed': 'yes', 'X-CSRF-TOKEN': state.session.csrfToken }
            });
            const payload = await upload.json().catch(() => ({}));
            if (!upload.ok) throw new Error(upload.status === 429 ? errorText('privacy-copy-busy') :
              errorText(upload.status === 413 ? 'privacy-copy-file-too-large' : payload.error));
            node.reset();
            response(payload.url);
            await refresh();
          }));
        }
        if (['pending', 'verified', 'released'].includes(copy.status)) {
          actions.push(button(tr('Revogar link', 'Revoke link', 'Revocar enlace'), async event => {
            if (!confirm(tr('Revogar os links deste pedido?', 'Revoke the links for this request?', '¿Revocar los enlaces de esta solicitud?'))) return;
            const control = event.currentTarget;
            control.disabled = true;
            try { await api(adminPath + '/' + copy.id, 'DELETE'); result.replaceChildren(); await refresh(); }
            catch (error) { feedback(error.message, true); }
            finally { control.disabled = false; }
          }));
        }
        return panel(copy.reference, p([status[copy.status], ' · ', evidence[copy.evidence], ' · ', date(copy.expiresAtUtc)]), actions);
      }));
    }

    const create = form([
      field(tr('Protocolo de privacidade', 'Privacy reference', 'Referencia de privacidad'),
        input('reference', 'text', '', { placeholder: 'SC-00000000', maxLength: 11 })),
      field(tr('Canal verificado', 'Verified channel', 'Canal verificado'),
        select('evidence', [['', tr('Selecione o canal', 'Select the channel', 'Selecciona el canal')], ...Object.entries(evidence)], '', { required: true })),
      field(tr('Conferi o canal em registros anteriores e confirmei que posso usá-lo para este pedido.',
        'I matched the channel to prior records and confirmed it can be used for this request.',
        'Cotejé el canal con registros anteriores y confirmé que se puede usar para esta solicitud.'),
        h('input', { type: 'checkbox', name: 'channelChecked', required: true }))
    ], tr('Gerar link de confirmação', 'Create confirmation link', 'Crear enlace de confirmación'), async data => {
      response((await api(adminPath, 'POST', data)).url);
      await refresh();
    });
    await refresh();
    return h('div', { class: 'form-stack' }, [
      panel(tr('Cópias complementares', 'Supplemental copies', 'Copias complementarias'),
        p(tr('Use o endereço original confirmado ou um canal independente já verificado. Se não houver prova suficiente da identidade, mantenha o pedido em atendimento.',
          'Use the confirmed original address or a previously verified independent channel. If there is insufficient proof of identity, keep the request under review.',
          'Usa la dirección original confirmada o un canal independiente ya verificado. Si faltan pruebas de identidad, mantén la solicitud en revisión.')),
        create, result, button(tr('Atualizar pedidos', 'Refresh requests', 'Actualizar solicitudes'), async () => {
          try { await refresh(); } catch (error) { feedback(error.message, true); }
        })),
      rows
    ]);
  }

  return { claimant, administration };
})();
