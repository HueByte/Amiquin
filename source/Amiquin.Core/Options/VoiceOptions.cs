namespace Amiquin.Core.Options;

/// <summary>
/// Configuration options for voice/TTS functionality.
/// </summary>
public class VoiceOptions
{
    public const string SectionName = "Voice";
    
    /// <summary>
    /// Name of the TTS model to use.
    /// </summary>
    public string TTSModelName { get; set; } = "en_GB-northern_english_male-medium";
    
    /// <summary>
    /// Command to execute Piper TTS.
    /// </summary>
    public string PiperCommand { get; set; } = "piper";
    
    /// <summary>
    /// Whether voice functionality is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}