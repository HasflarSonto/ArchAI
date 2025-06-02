from rhinomcp import get_rhino_connection

def test_grasshopper_basic():
    """
    Test basic Grasshopper functionality.
    """
    # Get the Rhino connection
    rhino = get_rhino_connection()
    
    # Create a script to check Grasshopper status
    script = """
import Rhino
import scriptcontext as sc

# Check if Grasshopper is loaded
try:
    import Grasshopper as gh
    print("Grasshopper is loaded")
    
    # Try to get the active canvas
    canvas = gh.Instances.ActiveCanvas
    if canvas:
        print("Active canvas found")
        print("Canvas name:", canvas.Name)
        print("Canvas document:", canvas.Document is not None)
    else:
        print("No active canvas found")
except ImportError:
    print("Grasshopper is not loaded")
"""
    
    # Execute the script
    print("Executing basic Grasshopper test...")
    result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
    print("Script result:", result)

if __name__ == "__main__":
    test_grasshopper_basic() 