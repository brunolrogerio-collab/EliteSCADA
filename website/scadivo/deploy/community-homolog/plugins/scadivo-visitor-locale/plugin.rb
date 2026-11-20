# frozen_string_literal: true

# name: scadivo-visitor-locale
# version: 1.1.0

require "http_accept_language"

module ScadivoVisitorLocale
  BROWSER_LOCALES = %w[pt-BR es en].freeze

  def anonymous_locale(request, **options)
    return super unless SiteSetting.set_locale_from_accept_language_header

    if options.fetch(:include_param, true) && SiteSetting.set_locale_from_param
      locale = request.params[Discourse::LOCALE_PARAM]
      return locale if locale.is_a?(String) && I18n.locale_available?(locale)
    end

    # In 2026.1 the browser preference was read directly from the cookie.
    locale =
      if respond_to?(:locale_from_cookie)
        locale_from_cookie(request)
      elsif SiteSetting.set_locale_from_cookie
        request.cookies["locale"]
      end
    return locale if locale.is_a?(String) && I18n.locale_available?(locale)

    # A quality of zero means the browser explicitly excludes this language.
    accepted =
      request.env["HTTP_ACCEPT_LANGUAGE"].to_s.split(",").reject do |entry|
        entry.match?(/;\s*q\s*=\s*0(?:\.0{0,3})?\s*\z/i)
      end
    parser = HttpAcceptLanguage::Parser.new(accepted.join(","))
    parser.compatible_language_from(BROWSER_LOCALES)&.tr("-", "_") || "en"
  end
end

module ScadivoCategoryDescriptionLocale
  def description_text
    description = translated_category_description
    return super unless description

    ERB::Util.html_escape(Nokogiri::HTML5.fragment(description).text.strip).html_safe
  end

  def description_excerpt
    description = translated_category_description
    description ? PrettyText.excerpt(PrettyText.cook(description), 300) : super
  end

  private

  def translated_category_description
    return if object.uncategorized? || !ContentLocalization.show_translated_category?(object, scope)

    object.get_localization&.description.presence
  end
end

after_initialize do
  Discourse.singleton_class.prepend(ScadivoVisitorLocale)
  # In 2026.1 the catalog still reads the original excerpts from this serializer.
  unless CategoryLocalization.method_defined?(:description_excerpt)
    SiteCategorySerializer.prepend(ScadivoCategoryDescriptionLocale)
  end
end
