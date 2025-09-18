using System.Text.Json.Serialization;

public class Prediction
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("width")]
    public double Width { get; set; }

    [JsonPropertyName("height")]
    public double Height { get; set; }

    [JsonPropertyName("class_name")]
    public string? Class { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
    private string? detectionId;

    public string? GetDetectionId()
    {
        return detectionId;
    }

    public void SetDetectionId(string? value)
    {
        detectionId = value;
    }
}
