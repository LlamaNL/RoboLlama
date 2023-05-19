namespace RoboLlamaLibrary.Plugins;

public interface ITriggerWordPlugin
{
    public Dictionary<string, Func<string, IEnumerable<string>>> GetTriggerWords();
}