namespace RoboLlama.Models;

public class ChannelStatus
{
    public string Name { get; set; }
    public string Status { get; set; }

    public ChannelStatus(string name, string status)
    {
        Name = name;
        Status = status;
    }
}