"""Who holds the advisory lease over a Unity Editor's state-changing operations."""

from fastmcp import Context

from models import MCPResponse
from services.registry import mcp_for_unity_resource
from services.tools import get_unity_instance_from_context
import transport.unity_transport as unity_transport
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_resource(
    uri="mcpforunity://editor/lease",
    name="agent_lease",
    description=(
        "Who currently holds this Unity Editor, since when, and until when. Always free to "
        "read, including while you are being denied — read it when a command comes back "
        "denied, and tell the user who holds the Editor rather than retrying blindly. "
        "Reports installed=false when no lease system is present, which is not an error.\n\n"
        "URI: mcpforunity://editor/lease"
    ),
)
async def get_agent_lease(ctx: Context) -> MCPResponse:
    unity_instance = await get_unity_instance_from_context(ctx)

    response = await unity_transport.send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "get_agent_lease",
        {},
    )

    if isinstance(response, dict) and not response.get("success", True):
        return MCPResponse(**response)

    data = response.get("data") if isinstance(response, dict) else None
    return MCPResponse(success=True, message="Retrieved agent lease.", data=data)
