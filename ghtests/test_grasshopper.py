from rhinomcp import get_rhino_connection
import json

def test_grasshopper():
    """
    Test basic Grasshopper functionality.
    """
    # Get the Rhino connection
    rhino = get_rhino_connection()
    
    # First, try to open Grasshopper
    print("Opening Grasshopper...")
    result = rhino.send_command("open_grasshopper", {})
    print("Open Grasshopper result:", result)
    
    # Try to export the current definition
    print("\nExporting current definition...")
    result = rhino.send_command("export_grasshopper_definition", {})
    print("Export result:", result)
    
    # Try to import a simple definition
    print("\nImporting test definition...")
    test_definition = {
        "components": {
            "panel": {
                "type": {
                    "full_name": "Grasshopper.Kernel.Components.Panel",
                    "is_plugin": False
                },
                "position": [100, 100],
                "properties": {
                    "name": "Test Panel"
                },
                "value": {
                    "text": "Hello World"
                }
            }
        }
    }
    result = rhino.send_command("import_grasshopper_definition", {"definition": test_definition})
    print("Import result:", result)

if __name__ == "__main__":
    test_grasshopper() 