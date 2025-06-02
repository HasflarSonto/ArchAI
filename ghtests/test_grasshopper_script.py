from rhinomcp import get_rhino_connection

def test_grasshopper_script():
    """
    Test Grasshopper functionality using RhinoScript.
    """
    # Get the Rhino connection
    rhino = get_rhino_connection()
    
    # Create a script to add a panel component
    script = """
import Rhino
import scriptcontext as sc
import ghpythonlib.components as ghcomp

# Get the active Grasshopper document
ghdoc = sc.doc.Objects.FindByType(Rhino.DocObjects.ObjectType.GrasshopperObject)[0]
if ghdoc:
    # Create a new panel component
    panel = ghcomp.Panel("Hello World")
    print("Panel created successfully")
else:
    print("No Grasshopper document found")
"""
    
    # Execute the script
    print("Executing RhinoScript...")
    result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
    print("Script result:", result)

if __name__ == "__main__":
    test_grasshopper_script() 