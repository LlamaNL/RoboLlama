namespace WebhookReceiver.Models.DTOs
{
    public class TwitchStreamAlertDto
    {
        public string UserName { get; set; }

        public string Title { get; set; }

        public int ViewerCount { get; set; }

        public string GameId { get; set; }

        public string ChannelId { get; set; }

        public bool Announced { get; set; }

        public bool TitleChanged { get; set; }
    }
}