namespace TextToSpeechApp.Interfaces;

public interface ITtsSaver
{
    Task GenerateWavAsync(string text, SpeechOptions options, string outputPath);
}
