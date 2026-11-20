import { apiInitializer } from "discourse/lib/api";
import ChatChannel from "discourse/plugins/chat/discourse/models/chat-channel";

function localized(channel) {
  const label = channel?.meta?.scadivo_localization;
  if (label?.source_title === channel?.title &&
      label?.source_description === channel?.description) {
    return label;
  }
}

function replaceText(element, value) {
  if (element && value && element.textContent.trim() !== value) {
    element.textContent = value;
  }
}

export default apiInitializer(() => {
  // Use the presentation getter for the chat heading and channel rows. Editing
  // still reads the original title and description from the channel model.
  const displayTitle = Object.getOwnPropertyDescriptor(ChatChannel.prototype, "displayTitle");
  Object.defineProperty(ChatChannel.prototype, "displayTitle", {
    ...displayTitle,
    get() {
      return localized(this)?.title ?? displayTitle.get.call(this);
    },
  });

  const models = new Map();
  const create = ChatChannel.create;
  ChatChannel.create = function (...args) {
    const channel = create.apply(this, args);
    if (channel.meta?.scadivo_localization) {
      models.set(String(channel.id), channel);
    }
    return channel;
  };

  // These two templates display the editable fields directly in 2026.1.
  const selector = 'a.sidebar-section-link[href*="/chat/c/"], .chat-channel-card[data-channel-id]';
  const refresh = () => {
    for (const element of document.querySelectorAll(selector)) {
      const id = element.dataset.channelId ?? element.getAttribute("href")?.match(/\/(\d+)(?:[/?#]|$)/)?.[1];
      const label = localized(models.get(id));
      if (!label) {
        continue;
      }
      if (element.matches(".chat-channel-card")) {
        replaceText(element.querySelector(".chat-channel-card__name"), label.title);
        replaceText(element.querySelector(".chat-channel-card__description"), label.description);
      } else {
        replaceText(element.querySelector(".sidebar-section-link-content-text"), label.title);
        if (element.title !== label.description) {
          element.title = label.description;
        }
      }
    }
  };

  // Channel panels mount after navigation, including when the mobile menu opens.
  const observer = new MutationObserver((records) => {
    if (records.some(({ target, addedNodes }) =>
      target.closest?.(selector) ||
      Array.from(addedNodes).some((node) => node.matches?.(selector) || node.querySelector?.(selector)),
    )) {
      refresh();
    }
  });
  observer.observe(document.body, { childList: true, subtree: true });
  refresh();
});
