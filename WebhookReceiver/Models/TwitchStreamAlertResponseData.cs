using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WebhookReceiver.Models
{
    public class TwitchStreamAlertResponseData
    {
        [JsonPropertyName("data")] public List<Alert> Data { get; set; }
    }

    public class Alert
    {
        [JsonPropertyName("id")] public string Id { get; set; }

        [JsonPropertyName("user_id")] public string UserId { get; set; }

        [JsonPropertyName("user_name")] public string UserName { get; set; }

        [JsonPropertyName("game_id")] public string GameId { get; set; }

        [JsonPropertyName("community_ids")] public object[] CommunityIds { get; set; }

        [JsonPropertyName("type")] public string Type { get; set; }

        [JsonPropertyName("title")] public string Title { get; set; }

        [JsonPropertyName("viewer_count")] public int ViewerCount { get; set; }

        [JsonPropertyName("started_at")] public DateTime StartedAt { get; set; }

        [JsonPropertyName("language")] public string Language { get; set; }

        [JsonPropertyName("thumbnail_url")] public string ThumbnailUrl { get; set; }
    }
}