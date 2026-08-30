using Newtonsoft.Json;

namespace MCPForUnity.Editor.Models
{
    /// <summary>
    /// Identifies the MCP client that issued a command.
    ///
    /// Several clients can drive one Editor at once with no awareness of each
    /// other. Until this field existed, every command from every client arrived
    /// indistinguishable, so nothing in the Editor could report — let alone
    /// arbitrate — concurrent agents.
    ///
    /// Populated by the server from the MCP session. A null Client is normal
    /// and must be treated as "unnamed but permitted": an older server process
    /// or a transport the server-side stamping missed would otherwise have
    /// every one of its commands refused.
    /// </summary>
    public class McpClientInfo
    {
        /// <summary>Stable id for the calling MCP session.</summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>Human-legible name, e.g. "amber-fox". Stable for the id.</summary>
        [JsonProperty("label")]
        public string Label { get; set; }

        /// <summary>Client program name when the peer supplied one.</summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>What to show a human. Never returns null or empty.</summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Label)) return Label;
                if (!string.IsNullOrWhiteSpace(Id)) return Id;
                return "unnamed agent";
            }
        }
    }
}
