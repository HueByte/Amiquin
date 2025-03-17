using System.Text.Json.Serialization;

namespace Amiquin.Core.Services.ApiClients.Responses;

public class NewsApiResponse
{
    [JsonPropertyName("data")]
    public Data? Data { get; set; }

    [JsonPropertyName("error")]
    public bool Error { get; set; }
}
public class Data
{
    [JsonPropertyName("min_news_id")]
    public string? MinNewsId { get; set; }

    [JsonPropertyName("news_list")]
    public List<NewsList>? NewsList { get; set; }

    [JsonPropertyName("reload_required")]
    public bool? ReloadRequired { get; set; }

    [JsonPropertyName("feed_type")]
    public string? FeedType { get; set; }
}

public class NewsList
{
    [JsonPropertyName("hash_id")]
    public string? HashId { get; set; }

    [JsonPropertyName("news_type")]
    public string? NewsType { get; set; }

    [JsonPropertyName("rank")]
    public int? Rank { get; set; }

    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("read_override")]
    public bool? ReadOverride { get; set; }

    [JsonPropertyName("fixed_rank")]
    public bool? FixedRank { get; set; }

    [JsonPropertyName("source_url")]
    public string? SourceUrl { get; set; }

    [JsonPropertyName("p_score")]
    public int? PScore { get; set; }

    [JsonPropertyName("publisher_interaction_meta")]
    public PublisherInteractionMeta PublisherInteractionMeta { get; set; }

    [JsonPropertyName("news_obj")]
    public NewsObj NewsObj { get; set; }
}

public class NewsObj
{
    [JsonPropertyName("old_hash_id")]
    public string? OldHashId { get; set; }

    [JsonPropertyName("hash_id")]
    public string? HashId { get; set; }

    [JsonPropertyName("author_name")]
    public string? AuthorName { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("source_url")]
    public string? SourceUrl { get; set; }

    [JsonPropertyName("source_name")]
    public string? SourceName { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("important")]
    public bool? Important { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("shortened_url")]
    public string? ShortenedUrl { get; set; }

    [JsonPropertyName("created_at")]
    public long? CreatedAt { get; set; }

    [JsonPropertyName("score")]
    public int? Score { get; set; }

    [JsonPropertyName("category_names")]
    public List<string?>? CategoryNames { get; set; }

    [JsonPropertyName("relevancy_tags")]
    public List<string?>? RelevancyTags { get; set; }

    [JsonPropertyName("tenant")]
    public string? Tenant { get; set; }

    [JsonPropertyName("fb_object_id")]
    public string? FbObjectId { get; set; }

    [JsonPropertyName("fb_like_count")]
    public int? FbLikeCount { get; set; }

    [JsonPropertyName("country_code")]
    public string? CountryCode { get; set; }

    [JsonPropertyName("impressive_score")]
    public double? ImpressiveScore { get; set; }

    [JsonPropertyName("targeted_city")]
    public List<object> TargetedCity { get; set; }

    [JsonPropertyName("gallery_image_urls")]
    public List<object> GalleryImageUrls { get; set; }

    [JsonPropertyName("full_gallery_urls")]
    public List<object> FullGalleryUrls { get; set; }

    [JsonPropertyName("bottom_headline")]
    public string? BottomHeadline { get; set; }

    [JsonPropertyName("bottom_text")]
    public string? BottomText { get; set; }

    [JsonPropertyName("darker_fonts")]
    public bool? DarkerFonts { get; set; }

    [JsonPropertyName("bottom_panel_link")]
    public string? BottomPanelLink { get; set; }

    [JsonPropertyName("bottom_type")]
    public string? BottomType { get; set; }

    [JsonPropertyName("version")]
    public int? Version { get; set; }

    [JsonPropertyName("position_start_time")]
    public DateTime? PositionStartTime { get; set; }

    [JsonPropertyName("position_expire_time")]
    public DateTime? PositionExpireTime { get; set; }

    [JsonPropertyName("trackers")]
    public List<object> Trackers { get; set; }

    [JsonPropertyName("dfp_tags")]
    public string? DfpTags { get; set; }

    [JsonPropertyName("dont_show_ad")]
    public bool? DontShowAd { get; set; }

    [JsonPropertyName("poll_tenant")]
    public string? PollTenant { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("show_inshorts_brand_name")]
    public bool? ShowInshortsBrandName { get; set; }

    [JsonPropertyName("crypto_coin_preference")]
    public object? CryptoCoinPreference { get; set; }

    [JsonPropertyName("is_overlay_supported")]
    public bool? IsOverlaySupported { get; set; }

    [JsonPropertyName("news_type")]
    public string? NewsType { get; set; }

    [JsonPropertyName("is_muted")]
    public bool? IsMuted { get; set; }

    [JsonPropertyName("video_audio_type")]
    public string? VideoAudioType { get; set; }

    [JsonPropertyName("auto_play_type")]
    public string? AutoPlayType { get; set; }

    [JsonPropertyName("show_in_video_feed_only")]
    public bool? ShowInVideoFeedOnly { get; set; }

    [JsonPropertyName("similar_threshold")]
    public int? SimilarThreshold { get; set; }

    [JsonPropertyName("is_similar_feed_available")]
    public bool? IsSimilarFeedAvailable { get; set; }

    [JsonPropertyName("publisher_info")]
    public PublisherInfo PublisherInfo { get; set; }

    [JsonPropertyName("show_publisher_info")]
    public bool? ShowPublisherInfo { get; set; }

    [JsonPropertyName("is_profile_clickable")]
    public bool? IsProfileClickable { get; set; }

    [JsonPropertyName("publisher_interaction_meta")]
    public PublisherInteractionMeta PublisherInteractionMeta { get; set; }

    [JsonPropertyName("show_capsule_image")]
    public bool? ShowCapsuleImage { get; set; }

    [JsonPropertyName("capsule_image_url")]
    public string? CapsuleImageUrl { get; set; }

    [JsonPropertyName("capsule_custom_card_id")]
    public string? CapsuleCustomCardId { get; set; }

    [JsonPropertyName("capsule_custom_card_url")]
    public string? CapsuleCustomCardUrl { get; set; }

    [JsonPropertyName("capsule_campaign")]
    public string? CapsuleCampaign { get; set; }

    [JsonPropertyName("is_youtube_video")]
    public object IsYoutubeVideo { get; set; }
}

public class PublisherInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("user_type")]
    public string? UserType { get; set; }

    [JsonPropertyName("profile_image_url")]
    public string? ProfileImageUrl { get; set; }

    [JsonPropertyName("thumbnail_image_url")]
    public string? ThumbnailImageUrl { get; set; }

    [JsonPropertyName("sponsored_text")]
    public string? SponsoredText { get; set; }
}

public class PublisherInteractionMeta
{
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("is_publisher_followed")]
    public bool? IsPublisherFollowed { get; set; }

    [JsonPropertyName("show_follow_button")]
    public bool? ShowFollowButton { get; set; }
}