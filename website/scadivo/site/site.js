(() => {
  const button = document.querySelector('.language-toggle');
  const nodes = [...document.querySelectorAll('[data-pt][data-en]')];
  let language = 'pt';

  function applyLanguage(next) {
    language = next;
    document.documentElement.lang = language === 'pt' ? 'pt-BR' : 'en';
    for (const node of nodes) {
      const value = node.dataset[language];
      if (node instanceof HTMLMetaElement) node.content = value;
      else node.textContent = value;
    }
    button.textContent = language === 'en' ? 'PT' : 'EN';
    button.setAttribute('aria-pressed', String(language === 'pt'));
    button.setAttribute('aria-label', language === 'en' ? 'Mudar idioma para português' : 'Switch language to English');
    try { localStorage.setItem('scadivo-site-language', language); } catch { /* Keep language switching available when storage is blocked. */ }
  }

  button.addEventListener('click', () => applyLanguage(language === 'en' ? 'pt' : 'en'));
  let saved;
  try { saved = localStorage.getItem('scadivo-site-language'); } catch { /* Portuguese remains the default. */ }
  applyLanguage(saved === 'en' ? 'en' : 'pt');
})();
