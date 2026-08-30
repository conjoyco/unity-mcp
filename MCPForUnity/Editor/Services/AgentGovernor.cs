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
    /// Registration point for the optional governor. With none registered the
    /// bridge behaves exactly as it did before: every command is allowed.
    /// </summary>
    public static class AgentGovernor
    {
        private static IAgentGovernor _current;

        /// <summary>The registered governor, or null when none is installed.</summary>
        public static IAgentGovernor Current => _current;

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
