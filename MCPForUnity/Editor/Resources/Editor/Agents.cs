using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Resources.Editor
{
    /// <summary>
    /// The MCP agents driving this Editor.
    ///
    /// A resource rather than a tool because it must stay free: an agent that
    /// has just been refused something, or one deciding whether to start a
    /// disruptive operation, needs to see the others without holding anything.
    ///
    /// The bridge keeps no roster of its own — it only knows an agent exists
    /// when a command arrives — so this forwards to whatever directory the
    /// policy layer registered.
    /// </summary>
    [McpForUnityResource("get_agents")]
    public static class Agents
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var directory = AgentGovernor.Directory;
                if (directory == null)
                {
                    return new SuccessResponse(
                        "No agent directory is installed.",
                        JObject.FromObject(AgentGovernor.NotInstalled("agent directory")));
                }

                var snapshot = directory.Agents(@params ?? new JObject());
                return new SuccessResponse("Retrieved agents.", JObject.FromObject(snapshot));
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error getting agents: {e.Message}");
            }
        }
    }
}
