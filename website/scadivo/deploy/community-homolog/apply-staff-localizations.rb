# frozen_string_literal: true

require "digest"
require "json"

package = JSON.parse(File.read(ARGV.fetch(0)))
staff = package.fetch("category")
topics = package.fetch("topics")
channels = package.fetch("channels")
raise "Expected a restricted category, two topics and two channels" unless
  staff.fetch("restricted") == true && topics.map { |entry| entry.fetch("slug") }.uniq.length == 2 &&
  channels.map { |entry| entry.fetch("slug") }.uniq.length == 2 && channels.count { |entry| entry.fetch("restricted") } == 1
raise "Enable manual content localization first" unless SiteSetting.content_localization_enabled

def translations(entry)
  values = entry.fetch("translations")
  raise "Expected English and Spanish" unless values.keys.sort == %w[en es]
  values
end

def save_translation(record, values)
  if record.persisted?
    raise "Existing translation differs. Review the private package." unless
      values.all? { |attribute, value| record.public_send(attribute) == value }
  else
    record.update!(values)
  end
end

ActiveRecord::Base.transaction do
  category = Category.lock.find_by!(slug: staff.fetch("slug"), read_restricted: true)
  raise "Restricted category text changed" unless
    category.name == staff.fetch("title") && category.description == staff.fetch("description")
  translations(staff).each do |locale, text|
    save_translation(
      category.localizations.find_or_initialize_by(locale: locale),
      name: text.fetch("title"),
      description: text.fetch("description"),
    )
  end

  topics.each do |entry|
    topic = Topic.lock.find_by!(category_id: category.id, slug: entry.fetch("slug"))
    post = topic.posts.lock.find_by!(post_number: 1)
    raise "Staff topic source changed" unless
      topic.title == entry.fetch("title") && Digest::SHA256.hexdigest(post.raw) == entry.fetch("digest")
    [topic, post].each do |record|
      raise "Unexpected source language" unless [nil, "pt_BR"].include?(record.locale)
      record.update!(locale: "pt_BR") unless record.locale
    end
    translations(entry).each do |locale, text|
      title = text.fetch("title")
      raw = text.fetch("body")
      localized_post = post.localizations.find_by(locale: locale)
      if localized_post
        raise "Existing post translation differs. Review the private package." unless
          localized_post.raw == raw && localized_post.post_version == post.version
      else
        PostLocalizationCreator.create(post: post, locale: locale, raw: raw, user: Discourse.system_user)
      end

      localized_topic = topic.localizations.find_by(locale: locale)
      if localized_topic
        raise "Existing topic translation differs. Review the private package." unless localized_topic.title == title
      else
        TopicLocalizationCreator.create(topic: topic, locale: locale, title: title, user: Discourse.system_user)
      end
    end
  end

  catalog = channels.to_h do |entry|
    channel = Chat::Channel.lock.find_by!(slug: entry.fetch("slug"), chatable_type: "Category")
    raise "Channel metadata or visibility changed" unless
      channel.name == entry.fetch("title") && channel.description == entry.fetch("description") &&
      channel.chatable.read_restricted == entry.fetch("restricted")
    [channel.id.to_s, entry.slice("title", "description", "restricted").merge("translations" => translations(entry))]
  end
  store = PluginStore.new("scadivo-channel-localization")
  previous = store.get("channels")
  raise "Existing channel translations differ. Review the private package." if previous && previous != catalog
  raise "Unable to save channel translations" if !previous && !store.set("channels", catalog)
end

puts "Applied two channel localizations and localized one restricted category and two topics. Sources preserved."
