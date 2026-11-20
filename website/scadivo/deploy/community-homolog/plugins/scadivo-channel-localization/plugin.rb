# frozen_string_literal: true

# name: scadivo-channel-localization
# version: 1.0.0

module ScadivoChannelLocalization
  STORE = "scadivo-channel-localization"
  KEY = "channels"

  module ChannelMetadata
    def meta
      payload = super
      locale = I18n.locale.to_s
      return payload unless %w[en es].include?(locale) && object.category_channel?

      request = scope.request
      return payload unless request
      return payload unless scope.can_preview_chat_channel?(object)

      catalog = request.env["scadivo.channel_localizations"] ||=
        PluginStore.get(STORE, KEY) || {}
      entry = catalog[object.id.to_s]
      return payload unless entry && entry[:restricted] == object.chatable.read_restricted
      return payload unless entry[:title] == object.name && entry[:description] == object.description

      translation = entry.fetch(:translations).fetch(locale)
      payload.merge(
        scadivo_localization: {
          source_title: object.name,
          source_description: object.description,
          title: translation.fetch(:title),
          description: translation.fetch(:description),
        },
      )
    end
  end
end

after_initialize do
  Chat::ChannelSerializer.prepend(ScadivoChannelLocalization::ChannelMetadata) if defined?(Chat::ChannelSerializer)
end
