from rhinomcp import get_rhino_connection

def test_grasshopper_factory():
    """
    Test Grasshopper functionality using the component factory.
    """
    # Get the Rhino connection
    rhino = get_rhino_connection()
    
    # Create a script to add a panel component using the factory
    script = """
import Rhino
import Grasshopper as gh
import scriptcontext as sc

# Get the active Grasshopper document
ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    # Create a new panel component using the factory
    panel = gh.Kernel.GH_Component.CreateComponent(
        "Panel",  # Component name
        "Panel",  # Nickname
        "A panel component",  # Description
        "Params",  # Category
        "Primitive",  # Subcategory
        gh.Kernel.GH_Exposure.primary,  # Exposure
        gh.Kernel.GH_Exposure.primary  # Exposure
    )
    
    if panel:
        # Set the panel's position
        panel.Attributes.Pivot = Rhino.Geometry.Point3d(100, 100, 0)
        
        # Add the component to the document
        ghdoc.AddObject(panel, False)
        print("Panel created successfully")
    else:
        print("Failed to create panel component")
else:
    print("No Grasshopper document found")

# Print available component types
print("\nAvailable component types:")
for obj in ghdoc.Objects:
    print(f"- {obj.Name} ({obj.TypeName})")
"""
    
    # Execute the script
    print("Executing Grasshopper factory test...")
    result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
    print("Script result:", result)

if __name__ == "__main__":
    test_grasshopper_factory() 