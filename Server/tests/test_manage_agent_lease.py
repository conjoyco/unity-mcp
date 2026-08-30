"""Tests for the manage_agent_lease tool."""
import asyncio
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

from services.tools.manage_agent_lease import manage_agent_lease


@pytest.fixture
def mock_unity(monkeypatch):
    captured: dict[str, object] = {}

    async def fake_send(send_fn, unity_instance, tool_name, params):
        captured["unity_instance"] = unity_instance
        captured["tool_name"] = tool_name
        captured["params"] = params
        return {"success": True, "message": "ok"}

    monkeypatch.setattr(
        "services.tools.manage_agent_lease.get_unity_instance_from_context",
        AsyncMock(return_value="unity-instance-1"),
    )
    monkeypatch.setattr(
        "services.tools.manage_agent_lease.send_with_unity_instance",
        fake_send,
    )
    return captured


@pytest.mark.parametrize("action", ["status", "acquire", "release"])
def test_actions_forward_to_unity(mock_unity, action):
    result = asyncio.run(manage_agent_lease(SimpleNamespace(), action=action))
    assert result["success"] is True
    assert mock_unity["tool_name"] == "manage_agent_lease"
    assert mock_unity["params"]["action"] == action


def test_reason_is_forwarded(mock_unity):
    asyncio.run(manage_agent_lease(SimpleNamespace(), action="acquire",
                                   reason="migrating trap prefabs"))
    assert mock_unity["params"]["reason"] == "migrating trap prefabs"


def test_ttl_is_forwarded_as_int(mock_unity):
    asyncio.run(manage_agent_lease(SimpleNamespace(), action="acquire", ttl_seconds=90))
    assert mock_unity["params"]["ttl_seconds"] == 90
    assert isinstance(mock_unity["params"]["ttl_seconds"], int)


def test_optional_params_are_omitted_when_unset(mock_unity):
    """Unity applies its own defaults; sending nulls would override them."""
    asyncio.run(manage_agent_lease(SimpleNamespace(), action="status"))
    assert "reason" not in mock_unity["params"]
    assert "ttl_seconds" not in mock_unity["params"]


def test_no_identity_parameter_is_accepted(mock_unity):
    """Identity comes from the command envelope, never from the caller.

    An agent that could name the holder could acquire or release on another
    agent's behalf, which would make the lease worthless as arbitration.
    """
    asyncio.run(manage_agent_lease(SimpleNamespace(), action="acquire"))
    params = mock_unity["params"]
    assert not {"client", "client_id", "label", "agent", "holder"} & set(params)
