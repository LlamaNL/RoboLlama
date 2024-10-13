using System;

namespace WebhookReceiver.Models.DTOs
{
    public class SubscriptionDto
    {
        public string ChannelId { get; set; }

        public DateTime RegistryDate { get; set; }

        public string ChannelName { get; set; }

        public string SubscriptionId { get; set; }
        public string UpdateId { get; set; }
    }
}