from mcp.server.fastmcp import Context
import json
from rhinomcp import get_rhino_connection, mcp, logger

@mcp.tool()
def open_grasshopper(ctx: Context) -> str:
    """Open Grasshopper in Rhino
    
    Returns:
    A message indicating whether Grasshopper was opened successfully.
    """
    try:
        # Get the global connection
        rhino = get_rhino_connection()

        # Send the command to open Grasshopper
        result = rhino.send_command("open_grasshopper", {})
        
        return f"Grasshopper opened successfully"
    except Exception as e:
        logger.error(f"Error opening Grasshopper: {str(e)}")
        return f"Error opening Grasshopper: {str(e)}" 