using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Models
{
    /// <summary>
    /// Represents a command received from the MCP client
    /// </summary>
    public class Command
    {
        /// <summary>
        /// The type of command to execute
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// The parameters for the command
        /// </summary>
        public JObject @params { get; set; }

        /// <summary>
        /// Which MCP client issued this command, when the server could identify
        /// one. Null is normal — see <see cref="McpClientInfo"/> — and means
        /// unnamed, never untrusted.
        /// </summary>
        public McpClientInfo client { get; set; }
    }
}

