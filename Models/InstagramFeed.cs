using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace project.Code.Models
{
    public partial class InstagramFeed
    {
        [JsonProperty("data")]
        public Datum[] Data { get; set; }

        [JsonProperty("paging")]
        public Paging Paging { get; set; }
    }

    public partial class Datum
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("caption")]
        public string Caption { get; set; }

        [JsonProperty("media_type")]
        public string MediaType { get; set; }

        [JsonProperty("media_url")]
        public string MediaUrl { get; set; }

        [JsonProperty("permalink")]
        public string Permalink { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
    }

    public partial class Paging
    {
        [JsonProperty("cursors")]
        public Cursors Cursors { get; set; }
    }

    public partial class Cursors
    {
        [JsonProperty("before")]
        public string Before { get; set; }

        [JsonProperty("after")]
        public string After { get; set; }
    }

}
