using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Models;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// The result of asking a governor whether a command may run.
    /// </summary>
    public readonly struct AgentVerdict
    {
        private AgentVerdict(bool allowed, string reason, object detail)
        {
            Allowed = allowed;
            Reason = reason;
            Detail = detail;
        }

        public bool Allowed { get; }

        /// <summary>Human-readable refusal, surfaced to the calling agent.</summary>
        public string Reason { get; }

        /// <summary>
        /// Structured refusal detail, serialised into the error response so the
        /// blocked agent can tell its user something true — who holds the lease,
        /// for how long, and whether waiting is reasonable. A bare "busy" gets
        /// retried in a loop or explained by invention.
        /// </summary>
        public object Detail { get; }

        public static AgentVerdict Allow() => new(true, null, null);

        public static AgentVerdict Deny(string reason, object detail = null)
            => new(false, string.IsNullOrWhiteSpace(reason) ? "Denied by agent governor." : reason, detail);
    }

    /// <summary>
    /// Observes and optionally refuses commands, so a policy layer outside this
    /// package can arbitrate between concurrent MCP clients.
    ///
    /// This exists because the dispatcher is the only point every command from
    /// every transport passes through, and it is internal. Rather than have the
    /// policy layer reflect into private state — which would fail silently on
    /// the next refactor, the worst outcome for a safety mechanism — the
    /// dispatcher offers this seam and keeps no policy of its own.
    /// </summary>
    public interface IAgentGovernor
    {
        /// <summary>
        /// Called for every command, before execution, on the main thread.
        /// Implementations must be cheap and must not throw; a throwing
        /// governor is unregistered rather than allowed to break the bridge.
        /// </summary>
        AgentVerdict Review(string commandType, JObject parameters, McpClientInfo client);
    }

    /// <summary>
    /// Optional companion to <see cref="IAgentGovernor"/>: reports the agents
    /// this Editor has seen. Split out so a governor can arbitrate without
    /// keeping a roster, and so the roster can exist before any arbitration
    /// does.
    /// </summary>
    public interface IAgentDirectory
    {
        /// <summary>
        /// A serialisable snapshot of the known agents. Shape is the policy
        /// layer's business; the bridge only passes it through.
        /// </summary>
        object Agents(JObject parameters);
    }

    /// <summary>
    /// Optional companion to <see cref="IAgentGovernor"/>: an advisory lease
    /// over state-changing operations.
    ///
    /// Declared here, ahead of any implementation, so that shipping the lease
    /// later costs an omu package bump rather than a second round trip through
    /// this frozen mirror — which every consumer project would have to repin.
    /// Until something implements it, the resource and tool below answer
    /// "not installed", which is the honest answer and not an error.
    /// </summary>
    public interface IAgentLease
    {
        /// <summary>Who holds the Editor, since when, and until when.</summary>
        object Status(JObject parameters);

        /// <summary>Take the lease explicitly, for a multi-step operation.</summary>
        object Acquire(McpClientInfo client, JObject parameters);

        /// <summary>Hand the lease back before it expires.</summary>
        object Release(McpClientInfo client, JObject parameters);
    }

    /// <summary>
    /// Registration point for the optional governor. With none registered the
    /// bridge behaves exactly as it did before: every command is allowed.
    /// </summary>
    public static class AgentGovernor
    {
        private static IAgentGovernor _current;

        /// <summary>The registered governor, or null when none is installed.</summary>
        public static IAgentGovernor Current => _current;

        /// <summary>The governor's roster, when it keeps one.</summary>
        public static IAgentDirectory Directory => _current as IAgentDirectory;

        /// <summary>The governor's lease, when it implements one.</summary>
        public static IAgentLease Lease => _current as IAgentLease;

        /// <summary>
        /// Standard answer for a surface nothing implements yet. Says which
        /// capability is missing rather than failing, so a caller can tell
        /// "no lease system here" from "the call went wrong".
        /// </summary>
        public static object NotInstalled(string capability) => new
        {
            installed = false,
            capability,
            message = $"No {capability} is installed in this Editor. " +
                      "Install or update the omu package to enable it.",
        };

        /// <summary>
        /// Install a governor. Domain reloads clear this, so implementations
        /// should register from an [InitializeOnLoad] static constructor.
        /// </summary>
        public static void Register(IAgentGovernor governor)
        {
            _current = governor ?? throw new ArgumentNullException(nameof(governor));
            McpLog.Info($"[AgentGovernor] Registered {governor.GetType().FullName}", false);
        }

        /// <summary>Remove the installed governor, restoring allow-everything.</summary>
        public static void Unregister(IAgentGovernor governor)
        {
            if (ReferenceEquals(_current, governor))
            {
                _current = null;
                McpLog.Info("[AgentGovernor] Unregistered", false);
            }
        }

        /// <summary>
        /// Ask the governor whether this command may run. Allows when no
        /// governor is installed, and allows — loudly — when one throws.
        ///
        /// Failing open is deliberate. A governor bug should cost arbitration,
        /// not the Editor's entire tool surface; and the error names the
        /// governor so the failure cannot pass unnoticed.
        /// </summary>
        public static AgentVerdict Review(string commandType, JObject parameters, McpClientInfo client)
        {
            var governor = _current;
            if (governor == null) return AgentVerdict.Allow();

            try
            {
                return governor.Review(commandType, parameters, client);
            }
            catch (Exception ex)
            {
                McpLog.Error(
                    $"[AgentGovernor] {governor.GetType().FullName} threw reviewing '{commandType}': {ex.Message}. " +
                    "Allowing the command and unregistering the governor.");
                Unregister(governor);
                return AgentVerdict.Allow();
            }
        }
    }
}
