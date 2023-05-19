
using System.Text.Json;

namespace WebhookReceiver.Extensions
{
    public static class ExtensionMethods
    {
        public static string PrintObject(this object input)
        {
            return JsonSerializer.Serialize(input);
        }
    }
}