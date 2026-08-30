"""The MCP agents driving a Unity Editor."""

from fastmcp import Context

from models import MCPResponse
from services.registry import mcp_for_unity_resource
from services.tools import get_unity_instance_from_context
import transport.unity_transport as unity_transport
from transport.legacy.unity_connection import async_send_command_with_retry


@mcp_for_unity_resource(
    uri="mcpforunity://editor/agents",
    name="agents",
    description=(
        "MCP agents that have issued commands to this Unity Editor, most recent first. "
        "Read this before starting anything disruptive — entering play mode, running tests, "
        "a long execute_code — when the answer names agents other than you.\n\n"
        "URI: mcpforunity://editor/agents"
    ),
)
async def get_agents(ctx: Context) -> MCPResponse:
    unity_instance = await get_unity_instance_from_context(ctx)

    response = await unity_transport.send_with_unity_instance(
        async_send_command_with_retry,
        unity_instance,
        "get_agents",
        {},
    )

    if isinstance(response, dict) and not response.get("success", True):
        return MCPResponse(**response)

    data = response.get("data") if isinstance(response, dict) else None
    return MCPResponse(success=True, message="Retrieved agents.", data=data)
