(() => {
  const form = document.querySelector('#contact-form');
  if (!form) return;

  const category = document.querySelector('#contact-category');
  const subject = document.querySelector('#contact-subject');
  const otherWrap = document.querySelector('#other-subject-wrap');
  const otherInput = document.querySelector('#other-subject');
  const status = document.querySelector('#contact-status');
  const submit = document.querySelector('#contact-submit');
  const subjects = {
    commercial: [
      ['commercial-info', 'Informações comerciais', 'Commercial information'],
      ['license', 'Licenciamento', 'Licensing'],
      ['demo', 'Solicitar demonstração', 'Request a demo'],
      ['partnership', 'Parceria', 'Partnership'],
      ['other', 'Outro', 'Other']
    ],
    support: [
      ['installation', 'Instalação', 'Installation'],
      ['configuration', 'Configuração', 'Configuration'],
      ['integration', 'Integração', 'Integration'],
      ['technical-problem', 'Problema técnico', 'Technical issue'],
      ['other', 'Outro', 'Other']
    ],
    contact: [
      ['general-info', 'Informações gerais', 'General information'],
      ['partnership', 'Parceria', 'Partnership'],
      ['press', 'Imprensa', 'Press'],
      ['privacy', 'Privacidade', 'Privacy'],
      ['other', 'Outro', 'Other']
    ]
  };

  function language() { return document.documentElement.lang === 'en' ? 'en' : 'pt'; }
  function updateSubjects() {
    const previous = subject.value;
    subject.replaceChildren(...subjects[category.value].map(([value, pt, en]) => {
      const option = document.createElement('option');
      option.value = value;
      option.textContent = language() === 'en' ? en : pt;
      return option;
    }));
    if ([...subject.options].some(option => option.value === previous)) subject.value = previous;
    const isOther = subject.value === 'other';
    otherWrap.hidden = !isOther;
    otherInput.required = isOther;
    if (!isOther) otherInput.value = '';
  }
  function setStatus(message, kind = '') {
    status.textContent = message;
    status.className = `contact-status ${kind}`;
  }

  const requestedCategory = new URLSearchParams(location.search).get('area');
  const categoryByQuery = { comercial: 'commercial', sales: 'commercial', suporte: 'support', support: 'support', contato: 'contact', contact: 'contact' };
  if (categoryByQuery[requestedCategory]) category.value = categoryByQuery[requestedCategory];
  category.addEventListener('change', updateSubjects);
  subject.addEventListener('change', updateSubjects);
  document.addEventListener('scadivo:languagechange', updateSubjects);
  updateSubjects();

  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (!form.reportValidity()) return;
    submit.disabled = true;
    setStatus(language() === 'en' ? 'Sending securely…' : 'Enviando com segurança…', 'pending');
    try {
      const sessionResponse = await fetch('/portal/api/session', { credentials: 'same-origin', cache: 'no-store' });
      if (!sessionResponse.ok) throw new Error('session');
      const session = await sessionResponse.json();
      if (!session.csrfToken) throw new Error('token');
      const values = new FormData(form);
      const response = await fetch('/portal/api/public/contact', {
        method: 'POST',
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': session.csrfToken },
        body: JSON.stringify({
          name: values.get('name'), email: values.get('email'), category: values.get('category'),
          subjectKey: values.get('subjectKey'), otherSubject: values.get('otherSubject'),
          message: values.get('message'), locale: language() === 'en' ? 'en' : 'pt-BR', website: values.get('website')
        })
      });
      if (!response.ok) throw new Error(response.status === 429 ? 'rate' : 'delivery');
      const result = await response.json();
      form.reset();
      if (categoryByQuery[requestedCategory]) category.value = categoryByQuery[requestedCategory];
      updateSubjects();
      setStatus(language() === 'en'
        ? `Message received. A confirmation is on its way to your email. Reference: ${result.reference}`
        : `Mensagem recebida. Uma confirmação está a caminho do seu e-mail. Protocolo: ${result.reference}`, 'success');
    } catch (error) {
      const message = error.message === 'rate'
        ? (language() === 'en' ? 'Please wait a few minutes before sending another message.' : 'Aguarde alguns minutos antes de enviar outra mensagem.')
        : (language() === 'en' ? 'We could not send your message right now. Please try again shortly.' : 'Não foi possível enviar sua mensagem agora. Tente novamente em instantes.');
      setStatus(message, 'error');
    } finally {
      submit.disabled = false;
    }
  });
})();
