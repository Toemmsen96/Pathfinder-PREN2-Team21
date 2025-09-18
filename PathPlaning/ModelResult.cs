using System.Text.Json.Serialization;

public class ModelResult
{
    [JsonPropertyName("predictions")]
    public List<Prediction>? Predictions { get; set; }
}