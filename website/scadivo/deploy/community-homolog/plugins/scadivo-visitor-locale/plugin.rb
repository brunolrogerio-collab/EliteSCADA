# frozen_string_literal: true

# name: scadivo-visitor-locale
# version: 1.0.0

require "http_accept_language"

module ScadivoVisitorLocale
  BROWSER_LOCALES = %w[pt-BR es en].freeze

  def anonymous_locale(request, **options)
    return super unless SiteSetting.set_locale_from_accept_language_header

    if options.fetch(:include_param, true) && SiteSetting.set_locale_from_param
      locale = request.params[Discourse::LOCALE_PARAM]
      return locale if locale.is_a?(String) && I18n.locale_available?(locale)
    end

    locale = locale_from_cookie(request)
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

after_initialize { Discourse.singleton_class.prepend(ScadivoVisitorLocale) }
