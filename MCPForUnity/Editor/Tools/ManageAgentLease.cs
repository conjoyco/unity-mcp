using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools
{
    /// <summary>
    /// Take or hand back the advisory lease over state-changing operations.
    ///
    /// Explicit verbs on top of implicit acquisition: an agent about to run a
    /// multi-step operation can hold the Editor across the whole of it instead
    /// of racing for it command by command, and an agent that finishes early
    /// can give it back rather than making everyone else wait out its TTL.
    ///
    /// The identity comes from the command envelope, not from a parameter. An
    /// agent must not be able to acquire or release on another's behalf, and
    /// the envelope is the one part of the request the agent does not author.
    /// </summary>
    [McpForUnityTool("manage_agent_lease", AutoRegister = false)]
    public static class ManageAgentLease
    {
        public static object HandleCommand(JObject @params)
        {
            @params ??= new JObject();

            string action = @params["action"]?.ToString()?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
            {
                return new ErrorResponse("Required parameter 'action' is missing. Expected: status, acquire, release.");
            }

            var lease = AgentGovernor.Lease;
            if (lease == null)
            {
                // Not an error: an Editor without a lease is the default, and a
                // caller should be able to tell that apart from a failed call.
                return new SuccessResponse("No agent lease is installed.",
                    AgentGovernor.NotInstalled("agent lease"));
            }

            var client = TransportCommandDispatcher.CurrentClient;

            try
            {
                switch (action)
                {
                    case "status":
                        return new SuccessResponse("Retrieved agent lease.", lease.Status(@params));

                    case "acquire":
                        if (client == null)
                        {
                            return new ErrorResponse(
                                "Cannot acquire a lease: this Editor could not identify the calling agent. " +
                                "The MCP server may predate agent identity; update it, or rely on implicit acquisition.");
                        }
                        return new SuccessResponse("Acquired agent lease.", lease.Acquire(client, @params));

                    case "release":
                        if (client == null)
                        {
                            return new ErrorResponse(
                                "Cannot release a lease: this Editor could not identify the calling agent.");
                        }
                        return new SuccessResponse("Released agent lease.", lease.Release(client, @params));

                    default:
                        return new ErrorResponse($"Unknown action '{action}'. Expected: status, acquire, release.");
                }
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error handling agent lease '{action}': {e.Message}");
            }
        }
    }
}
