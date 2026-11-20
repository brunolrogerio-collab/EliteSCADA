import assert from "node:assert/strict";
import { chromium, devices, expect } from "@playwright/test";

const portal = new URL(process.argv[2]);
const community = new URL(process.argv[3]);
const { SCADIVO_TEST_USERNAME, SCADIVO_TEST_PASSWORD } = process.env;
assert.ok(SCADIVO_TEST_USERNAME && SCADIVO_TEST_PASSWORD, "Set the test Portal credentials.");
assert.ok([portal, community].every(url => ["localhost", "127.0.0.1"].includes(url.hostname)),
  "Use the isolated homologation endpoints.");

const cases = [
  {
    profile: "pt-BR", browser: "en-GB", save: "Salvar idioma", community: "Comunidade",
    more: "Mais", chatSearch: "Pesquisar no chat", directMessage: "Nova mensagem direta",
    bulk: "Selecionar vários tópicos", skip: "Links para pular a navegação",
    headingLink: "Link para o título da seção",
    faqTitle: "Regras da comunidade SCADIVO", firstRule: /Regra 1 de homologação/,
    about: /Comunidade SCADIVO para quem estuda/, badge: "Primeiro link",
  },
  {
    profile: "en", browser: "pt-BR", save: "Save language", community: "Community",
    more: "More", chatSearch: "Search chat", directMessage: "Start new DM",
    bulk: "Bulk select", skip: "Skip navigation links", headingLink: "Heading link",
    faqTitle: "SCADIVO community rules", firstRule: /Respect others and keep discussions technical/,
    about: /SCADIVO community for people who study/, badge: "First Link",
  },
  {
    profile: "es", browser: "pt-BR", save: "Guardar idioma", community: "Comunidad",
    more: "Más", chatSearch: "Buscar en el chat", directMessage: "Nuevo mensaje directo",
    bulk: "Seleccionar varios temas", skip: "Enlaces para omitir la navegación",
    headingLink: "Enlace al encabezado",
    faqTitle: "Reglas de la comunidad SCADIVO", firstRule: /Respeta a los demás y mantén el enfoque técnico/,
    about: /Comunidad SCADIVO para quienes estudian/, badge: "Primer enlace",
  },
];

const browser = await chromium.launch();
try {
  for (const expected of cases) {
    const context = await browser.newContext({ ...devices["Pixel 7"], locale: expected.browser });
    try {
      const page = await context.newPage();
      await page.goto(portal.href);
      await page.locator('input[name="username"]').fill(SCADIVO_TEST_USERNAME);
      await page.locator('input[name="password"]').fill(SCADIVO_TEST_PASSWORD);
      const [login] = await Promise.all([
        page.waitForResponse(response => new URL(response.url()).pathname === "/portal/api/auth/login"),
        page.getByRole("button", { name: /^(Entrar|Sign in|Iniciar sesión)$/ }).click(),
      ]);
      assert.ok(login.ok(), "Portal sign-in returned HTTP " + login.status());
      await expect(page.getByRole("button", { name: /^(Sair|Sign out|Cerrar sesión)$/ })).toBeVisible();
      await page.getByRole("button", { name: /^(Minha conta|My account|Mi cuenta)$/ }).click();
      const selector = page.locator('select[name="locale"]');
      await selector.selectOption(expected.profile);
      await page.getByRole("button", { name: /^(Salvar idioma|Save language|Guardar idioma)$/ }).click();
      await page.getByText(/^(Idioma salvo\.|Language saved\.|Idioma guardado\.)$/).waitFor();
      await expect(page.getByRole("button", { name: expected.save, exact: true })).toBeVisible();
      await page.getByRole("button", { name: expected.community, exact: true }).click();
      const sso = page.locator('a[href$="/session/sso"]');
      const browserLanguage = await page.evaluate(() => navigator.language);
      assert.equal(browserLanguage, expected.browser);
      await sso.click();
      await page.waitForURL(url => url.origin === community.origin && url.pathname === "/");
      await expect(page.locator("#toggle-current-user")).toBeVisible();
      await expect(page.locator("html")).toHaveAttribute("lang", expected.profile);
      assert.ok(await page.evaluate(() => document.documentElement.clientWidth < 480 && matchMedia("(pointer: coarse)").matches), "Expected the mobile viewport and touch input.");
      await expect(page.locator("#skip-links__container")).toHaveAttribute("aria-label", expected.skip);
      await expect(page.locator("button.btn-default.bulk-select")).toHaveAccessibleName(expected.bulk);

      await page.locator("#toggle-current-user").click();
      await expect(page.getByRole("link", { name: new RegExp(expected.badge, "i") })).toBeVisible();
      await page.reload();

      await page.locator("#toggle-hamburger-menu").click();
      const sidebar = page.locator(".menu-panel.slide-in");
      await expect(sidebar.locator('a[href="/chat/search"]')).toHaveText(expected.chatSearch);
      await expect(sidebar.locator('a[href="/chat/new-message"]')).toHaveText(expected.directMessage);
      await sidebar.getByRole("button", { name: expected.more, exact: true }).click();
      await page.locator('a[href="/faq"]:visible').click({ timeout: 10000 });

      const faq = async () => {
        await expect(page).toHaveURL(new URL("/faq", community).href);
        const content = page.getByRole("main");
        await expect(content.getByRole("heading", { name: expected.faqTitle })).toBeVisible();
        await expect(content.locator("ol > li")).toHaveCount(6);
        await expect(content.locator("ol > li").first()).toHaveText(expected.firstRule);
        if (expected.profile === "pt-BR") {
          await expect(content.getByRole("link", { name: expected.headingLink })).toHaveCount(1);
        }
      };
      await faq();
      await page.reload();
      await faq();

      await page.goto(new URL("/about", community).href);
      await expect(page.getByText(expected.about)).toBeVisible();
      console.log(expected.profile + ": login via Portal, chat shortcuts, accessibility, notifications, FAQ and About passed.");
    } finally {
      await context.close();
    }
  }
} finally {
  await browser.close();
}
