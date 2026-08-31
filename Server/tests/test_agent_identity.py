"""Tests for per-MCP-session agent identity."""

import asyncio
import contextvars
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "src"))

from transport.agent_identity import (  # noqa: E402
    AgentIdentity,
    current_agent_wire,
    get_current_agent,
    identify_context,
    label_for,
    set_current_agent,
)


class _Ctx:
    """Minimal stand-in for a FastMCP context."""

    def __init__(self, session_id=None, client_id=None):
        self.session_id = session_id
        self.client_id = client_id


class _RequestCtxOnly:
    """A context that only exposes session_id via request_context."""

    def __init__(self, session_id):
        self.request_context = _Ctx(session_id=session_id)
        self.session_id = None
        self.client_id = None


def test_label_is_deterministic():
    assert label_for("abc123") == label_for("abc123")


def test_label_differs_between_sessions():
    labels = {label_for(f"session-{i}") for i in range(40)}
    # Not a uniqueness guarantee, just a sanity check that the label is
    # actually a function of the input and not a constant.
    assert len(labels) > 20


def test_label_survives_process_restart():
    """blake2b, not hash(): hash() is seeded per process and would renumber."""
    import subprocess

    src = Path(__file__).resolve().parents[1] / "src"
    code = (
        "import sys; sys.path.insert(0, %r);"
        "from transport.agent_identity import label_for; print(label_for('stable-session'))"
        % str(src)
    )
    out = subprocess.run(
        [sys.executable, "-c", code], capture_output=True, text=True, check=True
    )
    assert out.stdout.strip() == label_for("stable-session")


def test_empty_session_id_is_not_fatal():
    assert label_for("") == "unknown-agent"


def test_identity_wire_shape():
    identity = AgentIdentity.from_session("sess-1", client_name="claude-code")
    wire = identity.to_wire()
    assert wire["id"] == "sess-1"
    assert wire["label"] == label_for("sess-1")
    assert wire["name"] == "claude-code"


def test_wire_omits_absent_client_name():
    assert "name" not in AgentIdentity.from_session("sess-1").to_wire()


def test_identify_context_reads_session_id():
    identity = identify_context(_Ctx(session_id="sess-2", client_id="claude-code"))
    assert identity is not None
    assert identity.session_id == "sess-2"
    assert identity.client_name == "claude-code"


def test_identify_context_falls_back_to_request_context():
    identity = identify_context(_RequestCtxOnly("sess-3"))
    assert identity is not None
    assert identity.session_id == "sess-3"


@pytest.mark.parametrize("ctx", [None, _Ctx(), object()])
def test_unidentifiable_context_returns_none_rather_than_raising(ctx):
    """An unnamed caller is a normal outcome; it must never fail the request."""
    assert identify_context(ctx) is None


def test_current_agent_defaults_to_none():
    ctx = contextvars.copy_context()
    assert ctx.run(get_current_agent) is None
    assert ctx.run(current_agent_wire) is None


def test_set_and_read_current_agent():
    def run():
        set_current_agent(AgentIdentity.from_session("sess-4"))
        return current_agent_wire()

    wire = contextvars.copy_context().run(run)
    assert wire["id"] == "sess-4"


def test_identity_does_not_leak_between_contexts():
    """Two concurrent sessions must not see each other's identity."""

    def set_a():
        set_current_agent(AgentIdentity.from_session("agent-a"))
        return get_current_agent().session_id

    def read():
        agent = get_current_agent()
        return agent.session_id if agent else None

    ctx_a = contextvars.copy_context()
    ctx_b = contextvars.copy_context()
    assert ctx_a.run(set_a) == "agent-a"
    assert ctx_b.run(read) is None


def test_identity_survives_thread_hop_when_context_is_copied():
    """The stdio path hops to an executor thread; identity must survive it."""

    async def main():
        set_current_agent(AgentIdentity.from_session("sess-5"))
        loop = asyncio.get_running_loop()
        request_context = contextvars.copy_context()
        return await loop.run_in_executor(
            None, lambda: request_context.run(current_agent_wire)
        )

    wire = asyncio.run(main())
    assert wire is not None and wire["id"] == "sess-5"


def test_identity_is_lost_without_the_context_copy():
    """Guards the reason unity_connection copies the context explicitly."""

    async def main():
        set_current_agent(AgentIdentity.from_session("sess-6"))
        loop = asyncio.get_running_loop()
        return await loop.run_in_executor(None, current_agent_wire)

    assert asyncio.run(main()) is None
