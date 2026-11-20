# frozen_string_literal: true

require "json"

entries = JSON.parse(File.read(ARGV.fetch(0)))
slugs = entries.map { |entry| entry.fetch("slug") }
raise "Expected eight distinct public categories" unless slugs.length == 8 && slugs.uniq == slugs
raise "Enable manual content localization first" unless SiteSetting.content_localization_enabled

changed = 0
CategoryLocalization.transaction do
  entries.each do |entry|
    slug = entry.fetch("slug")
    category = Category.lock.find_by!(slug: slug, parent_category_id: nil, read_restricted: false)
    unless category.name == entry.fetch("sourceTitle") &&
             category.description == entry.fetch("sourceDescription")
      raise "Review translations for updated source category #{slug}"
    end

    translations = entry.fetch("translations")
    raise "Expected English and Spanish translations for #{slug}" unless translations.keys.sort == %w[en es]

    translations.each do |locale, fields|
      translation = category.localizations.find_or_initialize_by(locale: locale)
      translation.assign_attributes(name: fields.fetch("name"), description: fields.fetch("description"))
      next unless translation.changed?

      translation.save!
      changed += 1
    end
  end
end
Site.clear_anon_cache! if changed.positive?
puts "Checked #{entries.length} public categories. Saved #{changed} localization records."
