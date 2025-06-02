from mcp.server.fastmcp import Context
from rhinomcp import get_rhino_connection, mcp, logger
from typing import Any, Dict

@mcp.tool()
def export_grasshopper_definition(ctx: Context) -> Dict[str, Any]:
    """
    Export the current Grasshopper definition to a JSON representation.
    
    Returns:
    - A dictionary containing:
        - status: The status of the export operation
        - definition: The Grasshopper definition in JSON format (if successful)
        - message: Error message (if failed)
    
    The exported definition will include:
    - All components with their types, positions, and properties
    - All connections between components
    - Component values where applicable
    
    Example:
    ```python
    result = export_grasshopper_definition()
    if result["status"] == "success":
        definition = result["definition"]
        # Save to file or process further
    ```
    """
    try:
        # Get the global connection
        rhino = get_rhino_connection()
        
        # Send the command to export the definition
        return rhino.send_command("export_grasshopper_definition", {})
        
    except Exception as e:
        logger.error(f"Error exporting Grasshopper definition: {str(e)}")
        return {"status": "error", "message": str(e)} 