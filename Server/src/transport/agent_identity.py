"""Per-MCP-session agent identity, stamped onto every command sent to Unity.

Unity's command envelope carries no notion of who is calling: every command
from every connected client arrives over one socket, indistinguishable. That
makes concurrent agents invisible to each other by construction, which is the
whole problem the advisory-lease work exists to fix.

This module derives a stable, human-legible identity for the calling MCP
session and publishes it in a context variable. The transports read it when
they frame a command, so the identity reaches the Editor without every tool
signature having to thread it through.

Identity is keyed on ``ctx.session_id`` — the MCP-Session-Id header on HTTP, a
per-subprocess UUID on stdio. That is stable for the life of a Claude Code
session, which is the lifetime a lease holder needs. It is not stable across a
transport reconnect; see the design note for why that is tolerable for now.

The label is derived deterministically from the session id rather than assigned
in join order, so the same session gets the same name from any server process
that sees it, and a restarted server does not renumber everybody.
"""

from __future__ import annotations

import contextvars
import hashlib
from dataclasses import dataclass
from typing import Any

# Deliberately short, visually distinct, and safe to render in a Unity toolbar
# chip at small sizes. 32 x 32 = 1024 combinations before any collision is even
# possible, which is far past the number of agents anyone drives by hand.
_ADJECTIVES = (
    "amber", "brisk", "calm", "dusk", "eager", "fleet", "grave", "hazel",
    "ivory", "jade", "keen", "lucid", "mellow", "north", "opal", "prime",
    "quiet", "rapid", "slate", "terse", "umber", "vivid", "warm", "xenon",
    "young", "zesty", "bright", "clever", "dense", "even", "frank", "glad",
)

_NOUNS = (
    "fox", "hawk", "ibex", "jay", "kite", "lynx", "moth", "newt",
    "otter", "puma", "quail", "raven", "seal", "tern", "urchin", "viper",
    "wren", "yak", "zebu", "adder", "bison", "crane", "dove", "eagle",
    "finch", "gull", "heron", "koi", "loon", "mole", "owl", "pike",
)


def label_for(session_id: str) -> str:
    """Deterministic two-word label for a session id, e.g. ``amber-fox``.

    Stable across server restarts and across server processes, because it is a
    pure function of the id. Uses blake2b rather than ``hash()``, whose seed is
    randomised per process.
    """
    if not session_id:
        return "unknown-agent"
    digest = hashlib.blake2b(session_id.encode("utf-8"), digest_size=4).digest()
    value = int.from_bytes(digest, "big")
    return f"{_ADJECTIVES[value % len(_ADJECTIVES)]}-{_NOUNS[(value // len(_ADJECTIVES)) % len(_NOUNS)]}"


@dataclass(frozen=True)
class AgentIdentity:
    """Who is driving, as far as the server can tell."""

    session_id: str
    label: str
    client_name: str | None = None

    @classmethod
    def from_session(cls, session_id: str, client_name: str | None = None) -> "AgentIdentity":
        return cls(
            session_id=session_id,
            label=label_for(session_id),
            client_name=client_name,
        )

    def to_wire(self) -> dict[str, Any]:
        """The shape Unity receives on the command envelope's ``client`` field."""
        wire: dict[str, Any] = {"id": self.session_id, "label": self.label}
        if self.client_name:
            wire["name"] = self.client_name
        return wire


_current_agent: contextvars.ContextVar[AgentIdentity | None] = contextvars.ContextVar(
    "mcpforunity_current_agent", default=None
)


def set_current_agent(identity: AgentIdentity | None) -> contextvars.Token:
    """Publish the calling agent for the duration of this request."""
    return _current_agent.set(identity)


def get_current_agent() -> AgentIdentity | None:
    """The calling agent, or None when the caller could not be identified.

    Returning None is a normal outcome, not an error: an older server process,
    a transport this module does not cover, or an internal call made outside a
    request all land here. Callers must treat an unidentified caller as
    permitted-but-unnamed. Refusing unidentified commands would let a stale
    server process brick every tool call in the Editor.
    """
    return _current_agent.get()


def current_agent_wire() -> dict[str, Any] | None:
    """Convenience for transports framing a command."""
    identity = get_current_agent()
    return identity.to_wire() if identity is not None else None


def identify_context(ctx: Any) -> AgentIdentity | None:
    """Best-effort identity for a FastMCP context.

    Reads ``ctx.session_id`` (present on both transports) and, when the peer
    supplied one, the client name from ``ctx.client_id``. Never raises: an
    identity is a nice-to-have on the request path, and a failure to derive one
    must not fail the request.
    """
    if ctx is None:
        return None
    try:
        session_id = getattr(ctx, "session_id", None)
        if not session_id:
            request_ctx = getattr(ctx, "request_context", None)
            session_id = getattr(request_ctx, "session_id", None)
        if not session_id:
            return None
        client_name = getattr(ctx, "client_id", None)
        if client_name is not None:
            client_name = str(client_name)
        return AgentIdentity.from_session(str(session_id), client_name)
    except Exception:
        return None
