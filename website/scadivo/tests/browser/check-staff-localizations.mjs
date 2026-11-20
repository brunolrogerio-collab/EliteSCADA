import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { chromium, devices, expect } from "@playwright/test";

const portal = new URL(process.argv[2]);
const community = new URL(process.argv[3]);
assert.ok([portal, community].every(url => ["localhost", "127.0.0.1"].includes(url.hostname)),
  "Use isolated homologation endpoints.");
const fixture = JSON.parse(await readFile(process.argv[4], "utf8"));
const { SCADIVO_TEST_USERNAME, SCADIVO_TEST_PASSWORD, SCADIVO_TEST_RESTRICTED_PATHS } = process.env;
assert.ok(SCADIVO_TEST_USERNAME && SCADIVO_TEST_PASSWORD, "Set the test Portal credentials.");
const restricted = fixture.channels.find(channel => channel.restricted);
const order = [["pt-BR", "en-GB", "Comunidade"], ["en", "pt-BR", "Community"], ["es", "pt-BR", "Comunidad"]];
const localized = (entry, language) => language === "pt-BR" ? entry : entry.translations[language];
const plain = text => text.replace(/^#{1,6}\s+|^-\s+/gm, "")
  .replace(/\s+/g, " ").replace(/[’‘]/g, "'").trim();

async function assertDisplay(locator, expected, message) {
  await locator.waitFor({ state: "visible" });
  await expect.poll(async () => plain(await locator.innerText()) === plain(expected), { message }).toBe(true);
}

async function loginThroughPortal(page, language, browserLanguage, navigation) {
  await page.goto(portal.href);
  await page.locator('input[name="username"]').fill(SCADIVO_TEST_USERNAME);
  await page.locator('input[name="password"]').fill(SCADIVO_TEST_PASSWORD);
  const [login] = await Promise.all([
    page.waitForResponse(response => new URL(response.url()).pathname === "/portal/api/auth/login"),
    page.getByRole("button", { name: /^(Entrar|Sign in|Iniciar sesión)$/ }).click(),
  ]);
  assert.ok(login.ok(), "Portal sign-in failed");
  await page.getByRole("button", { name: /^(Minha conta|My account|Mi cuenta)$/ }).click();
  await page.locator('select[name="locale"]').selectOption(language);
  await page.getByRole("button", { name: /^(Salvar idioma|Save language|Guardar idioma)$/ }).click();
  await page.getByText(/^(Idioma salvo\.|Language saved\.|Idioma guardado\.)$/).waitFor();
  await page.getByRole("button", { name: navigation, exact: true }).click();
  assert.equal(await page.evaluate(() => navigator.language), browserLanguage);
  await page.locator('a[href$="/session/sso"]').click();
  await page.waitForURL(url => url.origin === community.origin && url.pathname === "/");
  await expect(page.locator("#toggle-current-user")).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("lang", language);
  assert.ok(await page.evaluate(() => document.documentElement.clientWidth < 480 && matchMedia("(pointer: coarse)").matches),
    "Expected a mobile viewport with touch input");
}

async function checkChannels(page, language, channels) {
  await page.locator("#toggle-hamburger-menu").click();
  for (const channel of channels) {
    const link = page.locator('.menu-panel.slide-in a[href*="/chat/c/' + channel.slug + '/"]');
    const expected = localized(channel, language);
    await assertDisplay(link.locator(".sidebar-section-link-content-text"), expected.title,
      "Incorrect localized channel label");
    await expect.poll(async () => await link.getAttribute("title") === expected.description,
      { message: "Incorrect localized channel description in sidebar" }).toBe(true);
  }

  const paths = [];
  for (const channel of channels) {
    const link = page.locator('.menu-panel.slide-in a[href*="/chat/c/' + channel.slug + '/"]');
    const address = await link.getAttribute("href");
    assert.ok(address, "Missing canonical channel address");
    paths.push(address);
  }

  await page.goto(new URL("/chat/browse", community).href);
  for (const [index, channel] of channels.entries()) {
    const card = page.locator(".chat-channel-card").filter({
      has: page.locator('a[href="' + paths[index] + '"]'),
    });
    const expected = localized(channel, language);
    await assertDisplay(card.locator(".chat-channel-card__name"), expected.title,
      "Incorrect localized channel name in directory");
    await assertDisplay(card.locator(".chat-channel-card__description"), expected.description,
      "Incorrect localized channel description in directory");
  }


  for (const [index, channel] of channels.entries()) {
    const channelUrl = new URL(paths[index], community);
    const response = await page.request.get(new URL("/chat/api/channels/" + channelUrl.pathname.split("/").pop(), community).href);
    assert.ok(response.ok(), "Unable to read the channel");
    const { channel: original } = await response.json();
    assert.ok(original.title === channel.title && original.description === channel.description,
      "The original editable channel fields changed");

    await page.goto(channelUrl.href);
    await assertDisplay(page.locator(".c-navbar__channel-title .chat-channel-name__label").filter({ visible: true }),
      localized(channel, language).title, "Incorrect localized title inside the channel");
  }

  return paths;
}

async function checkStaff(page, language) {
  const paths = await checkChannels(page, language, fixture.channels);
  const category = fixture.category;
  const response = await page.request.get(new URL("/categories.json", community).href);
  assert.ok(response.ok(), "Unable to read the authorized category catalog");
  const list = (await response.json()).category_list.categories;
  const found = list.find(item => item.slug === category.slug);
  assert.ok(found && found.read_restricted, "Missing restricted staff category");
  const expectedCategory = localized(category, language);
  assert.ok(found.name === expectedCategory.title && found.description === expectedCategory.description,
    "Wrong localized staff category data");

  const categoryPath = "/c/" + category.slug + "/" + found.id;

  await page.goto(new URL("/categories", community).href);
  const row = page.locator(".category-list-item").filter({
    has: page.locator('h3 a[href="' + categoryPath + '"]'),
  });
  await assertDisplay(row.locator("h3"), expectedCategory.title, "Wrong visible staff category title");
  await assertDisplay(row.locator(".category-description"), expectedCategory.description,
    "Wrong visible staff category description");
  await page.goto(new URL(categoryPath, community).href);
  const restrictedPaths = [categoryPath + ".json"];
  const privateChatPath = paths[fixture.channels.indexOf(restricted)];
  restrictedPaths.push("/chat/api/channels/" + privateChatPath.split("/").pop());

  for (const topic of fixture.topics) {
    const link = page.locator('a.title[href*="/t/' + topic.slug + '/"]').first();
    const expected = localized(topic, language);
    const href = await link.getAttribute("href");
    assert.ok(href, "Staff topic is missing in its category");
    const topicPath = new URL(href, community).pathname.replace(/(\/t\/[^/]+\/\d+).*/, "$1");
    restrictedPaths.push(topicPath + ".json");
    await page.goto(new URL(topicPath, community).href);
    await assertDisplay(page.locator("#topic-title h1 a"), expected.title, "Wrong staff topic title");
    const body = language === "pt-BR" ? topic.raw : expected.body;
    await assertDisplay(page.locator("#post_1 .cooked"), body, "Wrong staff topic body");
    await page.goto(new URL(categoryPath, community).href);
  }
  console.log(language + " passed for both channels, the restricted category, and both staff topics.");
  return restrictedPaths;
}

async function checkRestrictions(page, language) {
  const response = await page.request.get(new URL("/categories.json", community).href);
  assert.ok(response.ok(), "Unable to read the member category catalog");
  const list = (await response.json()).category_list.categories;
  assert.ok(!list.some(item => item.slug === fixture.category.slug), "Restricted category leaked");
  await checkChannels(page, language, fixture.channels.filter(channel => !channel.restricted));
  await page.goto(community.href);
  await page.locator("#toggle-hamburger-menu").click();
  assert.equal(await page.locator('.menu-panel.slide-in a[href*="/chat/c/' + restricted.slug + '/"]').count(), 0,
    "Restricted channel leaked into the sidebar");
  for (const path of JSON.parse(SCADIVO_TEST_RESTRICTED_PATHS)) {
    const denied = await page.request.get(new URL(path, community).href);
    assert.ok([401, 403, 404].includes(denied.status()), "Restricted direct access was not rejected");
  }
  console.log("Non-staff profile can use the localized public chat. Private category, channel and both topics are inaccessible.");
}

const browser = await chromium.launch();
try {
  for (const [language, browserLanguage, navigation] of SCADIVO_TEST_RESTRICTED_PATHS ? [order[2]] : order) {
    const context = await browser.newContext({ ...devices["Pixel 7"], locale: browserLanguage });
    try {
      const page = await context.newPage();
      await loginThroughPortal(page, language, browserLanguage, navigation);
      if (SCADIVO_TEST_RESTRICTED_PATHS) {
        await checkRestrictions(page, language);
      } else {
        const restrictedPaths = await checkStaff(page, language);
        if (language === "es") {
          console.log("Staging permission fixtures " + JSON.stringify(restrictedPaths));
        }
      }
    } finally {
      await context.close();
    }
  }
} finally {
  await browser.close();
}
