using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Resources.Editor
{
    /// <summary>
    /// Who currently holds the advisory lease over state-changing operations.
    ///
    /// Free to read, always: an agent that has just been denied needs to find
    /// out who is holding the Editor and for how long, and making that answer
    /// itself require the lease would be a deadlock by design.
    ///
    /// Answers "not installed" until a policy layer registers a lease. That is
    /// a normal state, not an error — the bridge itself arbitrates nothing.
    /// </summary>
    [McpForUnityResource("get_agent_lease")]
    public static class AgentLease
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var lease = AgentGovernor.Lease;
                if (lease == null)
                {
                    return new SuccessResponse(
                        "No agent lease is installed.",
                        AgentGovernor.NotInstalled("agent lease"));
                }

                return new SuccessResponse("Retrieved agent lease.", lease.Status(@params ?? new JObject()));
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error getting agent lease: {e.Message}");
            }
        }
    }
}
