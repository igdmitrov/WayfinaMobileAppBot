using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WayfinaMobileAppBot
{
    public class AccessTokenResponse
    {
        public string access_token { get; set; }
        public string scope { get; set; }
        public string api_domain { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
    }

    public class ZohoProduct
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? QuantityInStock { get; set; }
    }

    public class ZohoDeal
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        // Make sure this matches the exact API field name (case-sensitive)
        [JsonPropertyName("HubSpot_Id")]
        public string HubSpotId { get; set; }
    }

    public class ZohoDealsResponse
    {
        [JsonPropertyName("data")]
        public ZohoDeal[] Data { get; set; }

        [JsonPropertyName("info")]
        public JsonElement Info { get; set; }
    }

    public class ZohoCreateResponse
    {
        public List<ZohoCreateItem> data { get; set; } = new();
    }

    public class ZohoCreateItem
    {
        public string code { get; set; } = "";
        public ZohoDetails details { get; set; } = new();
        public string status { get; set; } = "";
    }

    public class ZohoDetails
    {
        public string id { get; set; } = "";
    }
}
