from mcp.server.fastmcp import Context
from rhinomcp import get_rhino_connection, mcp, logger
from typing import Any, Dict
import json

@mcp.tool()
def import_grasshopper_definition(ctx: Context, definition: Dict[str, Any]) -> Dict[str, Any]:
    """
    Import a Grasshopper definition from a JSON representation.
    
    Parameters:
    - definition: A dictionary containing the Grasshopper definition in JSON format.
                 This should include components, connections, and their properties.
    
    Returns:
    - A dictionary with the status of the import operation.
    
    Example:
    ```python
    definition = {
        "components": {
            "comp1": {
                "type": {"full_name": "Grasshopper.Kernel.Components.Panel", "is_plugin": False},
                "position": [100, 100],
                "properties": {"name": "My Panel", "description": "A test panel"},
                "value": {"text": "Hello World"}
            }
        }
    }
    result = import_grasshopper_definition(definition)
    ```
    """
    try:
        # Get the global connection
        rhino = get_rhino_connection()
        
        # Send the command to import the definition
        return rhino.send_command("import_grasshopper_definition", {"definition": definition})
        
    except Exception as e:
        logger.error(f"Error importing Grasshopper definition: {str(e)}")
        return {"status": "error", "message": str(e)} 