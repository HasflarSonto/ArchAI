from rhinomcp import get_rhino_connection

def test_grasshopper_python5():
    """
    Test Grasshopper functionality using the Grasshopper component factory with a different method.
    """
    # Get the Rhino connection
    rhino = get_rhino_connection()
    
    # Create a script to add components using Python
    script = """
import Rhino
import Grasshopper as gh
import scriptcontext as sc
import ghpythonlib.components as ghcomp
import ghpythonlib.parallel

# Get the active Grasshopper document
ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    # Create a new Python component using the factory
    python_comp = gh.Kernel.GH_Component.CreateComponent(
        "Python",  # Component name
        "Python",  # Nickname
        "A Python component",  # Description
        "Script",  # Category
        "Python",  # Subcategory
        gh.Kernel.GH_Exposure.primary,  # Exposure
        gh.Kernel.GH_Exposure.primary  # Exposure
    )
    
    if python_comp:
        # Set the component's position
        python_comp.Attributes.Pivot = Rhino.Geometry.Point3d(100, 100, 0)
        
        # Add the component to the document
        ghdoc.AddObject(python_comp, False)
        
        # Set the Python code
        python_code = '''
import ghpythonlib.components as ghcomp

# Create a panel
panel = ghcomp.Panel("Hello from Python!")
output = panel
'''
        python_comp.SetScript(python_code)
        
        print("Python component created successfully")
    else:
        print("Failed to create Python component")
else:
    print("No Grasshopper document found")

# Print available component types
print("\nAvailable component types:")
for obj in ghdoc.Objects:
    print(f"- {obj.Name} ({obj.TypeName})")
"""
    
    # Execute the script
    print("Executing Grasshopper Python test...")
    result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
    print("Script result:", result)

if __name__ == "__main__":
    test_grasshopper_python5() 