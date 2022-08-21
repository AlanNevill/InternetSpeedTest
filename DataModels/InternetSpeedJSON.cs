using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace InternetSpeedTest.DataModels;

internal class InternetSpeedJSON
{
    // Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
    public class Download
    {
        [JsonPropertyName("bandwidth")]
        public int Bandwidth { get; set; }

        [JsonPropertyName("bytes")]
        public int Bytes { get; set; }

        [JsonPropertyName("elapsed")]
        public int Elapsed { get; set; }

        [JsonPropertyName("latency")]
        public Latency? Latency { get; set; }
    }

    public class Interface
    {
        [JsonPropertyName("internalIp")]
        public string InternalIp { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("macAddr")]
        public string MacAddr { get; set; }

        [JsonPropertyName("isVpn")]
        public bool IsVpn { get; set; }

        [JsonPropertyName("externalIp")]
        public string ExternalIp { get; set; }
    }

    public class Latency
    {
        [JsonPropertyName("iqm")]
        public double Iqm { get; set; }

        [JsonPropertyName("low")]
        public double Low { get; set; }

        [JsonPropertyName("high")]
        public double High { get; set; }

        [JsonPropertyName("jitter")]
        public double Jitter { get; set; }
    }

    public class Ping
    {
        [JsonPropertyName("jitter")]
        public double Jitter { get; set; }

        [JsonPropertyName("latency")]
        public double Latency { get; set; }

        [JsonPropertyName("low")]
        public double Low { get; set; }

        [JsonPropertyName("high")]
        public double High { get; set; }
    }

    public class Result
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("persisted")]
        public bool Persisted { get; set; }
    }

    public class Root
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("ping")]
        public Ping Ping { get; set; }

        [JsonPropertyName("download")]
        public Download Download { get; set; }

        [JsonPropertyName("upload")]
        public Upload Upload { get; set; }

        [JsonPropertyName("isp")]
        public string Isp { get; set; }

        [JsonPropertyName("interface")]
        public Interface Interface { get; set; }

        [JsonPropertyName("server")]
        public Server Server { get; set; }

        [JsonPropertyName("result")]
        public Result Result { get; set; }
    }

    public class Server
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("host")]
        public string Host { get; set; }

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("ip")]
        public string Ip { get; set; }
    }

    public class Upload
    {
        [JsonPropertyName("bandwidth")]
        public int Bandwidth { get; set; }

        [JsonPropertyName("bytes")]
        public int Bytes { get; set; }

        [JsonPropertyName("elapsed")]
        public int Elapsed { get; set; }

        [JsonPropertyName("latency")]
        public Latency Latency { get; set; }
    }


}
