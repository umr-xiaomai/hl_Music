using System.Text.Json.Serialization;

namespace KuGou.Net.Abstractions.Models;

public record  QRCode : KgBaseModel
{
    [JsonPropertyName("qrcode")] public string Qrcode { get; set; }

    [JsonPropertyName("qrcode_img")] public string QrcodeImg { get; set; }
}