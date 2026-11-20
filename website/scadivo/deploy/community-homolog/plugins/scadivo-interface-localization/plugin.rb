# frozen_string_literal: true

# name: scadivo-interface-localization
# version: 1.0.0

module ScadivoInterfaceLocalization
  SITE_DESCRIPTION =
    "Comunidade SCADIVO para quem estuda, integra e opera sistemas de automação e supervisão."

  BADGE_NAMES = { 10 => "Editor", 14 => "First Link" }.freeze

  module Faq
    # Discourse 2026.1 renders the original cooked post on the static FAQ.
    # Follow the same localization and original-text preference as a topic.
    def render(*args, **options, &block)
      if @page == "faq" && @topic && args.first == :show && @body.present?
        post = @topic.first_post
        if post && ContentLocalization.show_translated_post?(post, guardian)
          translation = post.get_localization
          if translation&.cooked.present?
            @body = translation.cooked
            if @topic.get_localization&.title.present?
              fragment = Nokogiri::HTML5.fragment(@body)
              unless fragment.at_css("h1")
                heading = Nokogiri::XML::Node.new("h1", fragment.document)
                heading.content = @topic.get_localization.title
                fragment.prepend_child(heading)
                @body = fragment.to_html
              end
            end
          end
        end

        label = ERB::Util.html_escape(I18n.t("js.post.heading_anchor"))
        @body = @body.gsub('aria-label="Heading link"', %(aria-label="#{label}"))
      end

      super
    end
  end

  module About
    def description
      source = object.description
      return source unless source == SITE_DESCRIPTION && %w[en es].include?(I18n.locale.to_s)

      I18n.t("scadivo_interface.site_description", default: source)
    end
  end

  module BadgeNotification
    def data
      payload = super
      return payload unless object.notification_type == Notification.types[:granted_badge]

      name = BADGE_NAMES[payload["badge_id"].to_i]
      return payload unless name

      # Notification data stores the label at award time. Keep its badge ID,
      # slug, and all other fields, and resolve the label for the reader.
      payload.merge("badge_name" => Badge.display_name(name))
    end
  end
end

after_initialize do
  StaticController.prepend(ScadivoInterfaceLocalization::Faq)
  AboutSerializer.prepend(ScadivoInterfaceLocalization::About)
  NotificationSerializer.prepend(ScadivoInterfaceLocalization::BadgeNotification)
end
