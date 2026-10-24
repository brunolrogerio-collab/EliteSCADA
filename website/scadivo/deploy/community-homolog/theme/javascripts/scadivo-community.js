import { apiInitializer } from "discourse/lib/api";

const normalizeLocale = (value) => {
  const locale = String(value || "").replaceAll("_", "-").toLowerCase();
  if (locale.startsWith("en")) {
    return "en";
  }
  if (locale.startsWith("es")) {
    return "es";
  }
  return "pt_BR";
};

const cookieLocale = () => {
  const entry = document.cookie
    .split(";")
    .map((value) => value.trim())
    .find((value) => value.startsWith("locale="));
  return entry ? decodeURIComponent(entry.slice("locale=".length)) : "";
};

export default apiInitializer((api) => {
  const updateBrandAndLanguage = () => {
    const brandLink = document.querySelector(".d-header .title");
    const brandImage = brandLink?.querySelector("img");
    if (brandLink) {
      brandLink.setAttribute("aria-label", "SCADIVO");
      brandLink.setAttribute("title", "SCADIVO");
    }
    brandImage?.setAttribute("alt", "SCADIVO");

    const languageBar = document.querySelector(".scadivo-community-language-bar");
    const picker = document.querySelector("#scadivo-community-language");
    if (!languageBar || !picker) {
      return;
    }

    const authenticated = Boolean(api.getCurrentUser());
    languageBar.classList.toggle("is-authenticated", authenticated);
    if (authenticated) {
      return;
    }

    const queryLocale = new URLSearchParams(window.location.search).get("tl");
    picker.value = normalizeLocale(
      queryLocale || cookieLocale() || document.documentElement.lang
    );

    if (picker.dataset.scadivoBound === "true") {
      return;
    }
    picker.dataset.scadivoBound = "true";
    picker.addEventListener("change", () => {
      const url = new URL(window.location.href);
      url.searchParams.set("tl", picker.value);
      window.location.assign(url.toString());
    });
  };

  updateBrandAndLanguage();
  api.onPageChange(updateBrandAndLanguage);
});
