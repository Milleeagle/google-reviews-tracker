using Newtonsoft.Json;

namespace GoogleReviews.Core.Models;

// Legacy API models (kept for backward compatibility)
public class PlaceDetailsResponse
{
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("result")]
    public PlaceResult? Result { get; set; }
}

public class PlaceResult
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("rating")]
    public double Rating { get; set; }

    [JsonProperty("user_ratings_total")]
    public int UserRatingsTotal { get; set; }

    [JsonProperty("reviews")]
    public List<PlaceReview>? Reviews { get; set; }
}

public class PlaceReview
{
    [JsonProperty("author_name")]
    public string AuthorName { get; set; } = string.Empty;

    [JsonProperty("author_url")]
    public string? AuthorUrl { get; set; }

    [JsonProperty("profile_photo_url")]
    public string? ProfilePhotoUrl { get; set; }

    [JsonProperty("rating")]
    public int Rating { get; set; }

    [JsonProperty("relative_time_description")]
    public string? RelativeTimeDescription { get; set; }

    [JsonProperty("text")]
    public string? Text { get; set; }

    [JsonProperty("time")]
    public long Time { get; set; }
}

// New Places API models
public class NewPlacesApiResponse
{
    [JsonProperty("displayName")]
    public DisplayName? DisplayName { get; set; }

    [JsonProperty("rating")]
    public double Rating { get; set; }

    [JsonProperty("userRatingCount")]
    public int UserRatingCount { get; set; }

    [JsonProperty("reviews")]
    public List<NewPlaceReview>? Reviews { get; set; }
}

public class DisplayName
{
    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    [JsonProperty("languageCode")]
    public string LanguageCode { get; set; } = string.Empty;
}

public class NewPlaceReview
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("relativePublishTimeDescription")]
    public string RelativePublishTimeDescription { get; set; } = string.Empty;

    [JsonProperty("rating")]
    public int Rating { get; set; }

    [JsonProperty("text")]
    public ReviewText? Text { get; set; }

    [JsonProperty("originalText")]
    public ReviewText? OriginalText { get; set; }

    [JsonProperty("authorAttribution")]
    public AuthorAttribution? AuthorAttribution { get; set; }

    [JsonProperty("publishTime")]
    public string PublishTime { get; set; } = string.Empty;
}

public class ReviewText
{
    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;

    [JsonProperty("languageCode")]
    public string LanguageCode { get; set; } = string.Empty;
}

public class AuthorAttribution
{
    [JsonProperty("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonProperty("uri")]
    public string? Uri { get; set; }

    [JsonProperty("photoUri")]
    public string? PhotoUri { get; set; }
}