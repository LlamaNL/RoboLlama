using System;
using System.Text.Json.Serialization;

namespace WebhookReceiver.Models
{
    //{
    //    "subscription": {
    //        "id": "f1c2a387-161a-49f9-a165-0f21d7a4e1c4",
    //        "type": "stream.online",
    //        "version": "1",
    //        "status": "enabled",
    //        "cost": 0,
    //        "condition": {
    //            "broadcaster_user_id": "1337"
    //        },
    //         "transport": {
    //            "method": "webhook",
    //            "callback": "https://example.com/webhooks/callback"
    //        },
    //        "created_at": "2019-11-16T10:11:12.123Z"
    //    },
    //    "event": {
    //        "id": "9001",
    //        "broadcaster_user_id": "1337",
    //        "broadcaster_user_login": "cool_user",
    //        "broadcaster_user_name": "Cool_User",
    //        "type": "live",
    //        "started_at": "2020-10-11T10:11:12.123Z"
    //    }
    //}

    public class TwitchEventSubResponse
    {
        [JsonPropertyName("challenge")]
        public string Challenge { get; set; }

        [JsonPropertyName("subscription")]
        public TwitchSubscription Subscription { get; set; }

        [JsonPropertyName("event")]
        public Event Event { get; set; }
    }

    public class Event
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }

        [JsonPropertyName("broadcaster_user_login")]
        public string BroadcasterUserLogin { get; set; }

        [JsonPropertyName("broadcaster_user_name")]
        public string BroadcasterUserName { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("started_at")]
        public DateTimeOffset StartedAt { get; set; }
    }

    public class TwitchSubscription
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("cost")]
        public long Cost { get; set; }

        [JsonPropertyName("condition")]
        public Condition Condition { get; set; }

        [JsonPropertyName("transport")]
        public Transport Transport { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class Condition
    {
        [JsonPropertyName("broadcaster_user_id")]
        public string BroadcasterUserId { get; set; }
    }

    public class Transport
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("callback")]
        public Uri Callback { get; set; }
    }
}
