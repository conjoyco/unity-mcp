"""Take or hand back the advisory lease over a Unity Editor."""

from typing import Annotated, Any, Literal

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_tool(
    name="manage_agent_lease",
    description=(
        "Take or hand back the advisory lease over this Unity Editor's state-changing "
        "operations. Only needed for a multi-step operation you want to hold the Editor "
        "across, or to give the lease back early; ordinary calls take and release it "
        "implicitly. Read mcpforunity://editor/lease for status without changing anything."
    ),
    annotations=ToolAnnotations(
        title="Manage Agent Lease",
        destructiveHint=False,
        idempotentHint=True,
    ),
)
async def manage_agent_lease(
    ctx: Context,
    action: Annotated[Literal["status", "acquire", "release"],
                      "status reports the holder; acquire takes the lease for this session; "
                      "release hands it back."],
    reason: Annotated[str | None,
                      "Short human-readable note shown to anyone who is denied while you hold it, "
                      "e.g. 'migrating trap prefabs'. Strongly recommended on acquire."] = None,
    ttl_seconds: Annotated[int | None,
                           "How long to hold it without further activity. Server default applies "
                           "when omitted; activity renews it either way."] = None,
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)

    params: dict[str, Any] = {"action": action}
    if reason is not None:
        params["reason"] = reason
    if ttl_seconds is not None:
        params["ttl_seconds"] = int(ttl_seconds)

    return await send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "manage_agent_lease",
        params,
    )
