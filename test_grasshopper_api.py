from rhinomcp import get_rhino_connection

def test_grasshopper_api():
    """
    Test Grasshopper functionality by creating a number slider component.
    """
    # Get the Rhino connection
    rhino = get_rhino_connection()
    
    # Create a script to add a number slider component using the Grasshopper API
    script = """
import Rhino
import Grasshopper as gh
import scriptcontext as sc
import System.Drawing

# Get the active Grasshopper document
ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    # Create a new number slider component
    slider = gh.Kernel.Special.GH_NumberSlider()
    
    if slider:
        # Create and set attributes
        slider.CreateAttributes()
        slider.Attributes.Pivot = System.Drawing.PointF(100, 100)
        
        # Set default values
        slider.Slider.Minimum = 0.0
        slider.Slider.Maximum = 100.0
        slider.Slider.Value = 50.0
        
        # Add the component to the document
        ghdoc.AddObject(slider, False)
        print("Number slider created successfully")
    else:
        print("Failed to create number slider component")
else:
    print("No Grasshopper document found")
"""
    
    # Execute the script
    print("Executing Grasshopper API script...")
    result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
    print("Script result:", result)

def create_populate3d_component():
    """
    Test Grasshopper functionality by creating a Populate 3D component.
    """
    rhino = get_rhino_connection()
    script = """
import Rhino
import Grasshopper as gh
import scriptcontext as sc
import System.Drawing
import System

# GUID for Populate 3D component
POPULATE3D_GUID = "e202025b-dc8e-4c51-ae19-4415b172886f"

# Get the active Grasshopper document
ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    # Find the Populate 3D component by GUID
    proxy = None
    for obj in gh.Instances.ComponentServer.ObjectProxies:
        if str(getattr(obj, 'Guid', '')) == POPULATE3D_GUID:
            proxy = obj
            break
    if proxy is None:
        print("Populate 3D component not found by GUID.")
    else:
        component = proxy.CreateInstance()
        component.CreateAttributes()
        component.Attributes.Pivot = System.Drawing.PointF(200, 100)
        ghdoc.AddObject(component, False)
        print("Populate 3D component created successfully")
else:
    print("No Grasshopper document found")
"""
    print("Executing Grasshopper API script for Populate 3D...")
    result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
    print("Script result:", result)

def create_point_component_with_internalized_points():
    """
    Test Grasshopper functionality by creating a Point component with 4 internalized points using Append on PersistentData.
    """
    rhino = get_rhino_connection()
    script = """
import Rhino
import Grasshopper as gh
import scriptcontext as sc
import System.Drawing
import System

# GUID for Point component
POINT_GUID = "fbac3e32-f100-4292-8692-77240a42fd1a"

# Get the active Grasshopper document
ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    # Find the Point component by GUID
    proxy = None
    for obj in gh.Instances.ComponentServer.ObjectProxies:
        if str(getattr(obj, 'Guid', '')) == POINT_GUID:
            proxy = obj
            break
    if proxy is None:
        print("Point component not found by GUID.")
    else:
        component = proxy.CreateInstance()
        component.CreateAttributes()
        component.Attributes.Pivot = System.Drawing.PointF(300, 100)
        
        # Create 4 points
        points = [
            Rhino.Geometry.Point3d(0, 0, 0),
            Rhino.Geometry.Point3d(10, 0, 0),
            Rhino.Geometry.Point3d(10, 10, 0),
            Rhino.Geometry.Point3d(0, 10, 0)
        ]
        
        # Convert to GH_Points
        gh_points = [gh.Kernel.Types.GH_Point(pt) for pt in points]
        
        # Add each point to PersistentData using Append
        for gh_point in gh_points:
            component.PersistentData.Append(gh_point)
        
        # Force the component to update
        component.ExpireSolution(True)
        
        ghdoc.AddObject(component, False)
        print("Point component created successfully with 4 internalized points")
else:
    print("No Grasshopper document found")
"""
    print("Executing Grasshopper API script for Point component...")
    result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
    print("Script result:", result)

def connect_slider_to_populate3d():
    """
    Connect the number slider output to the count on the Populate 3D component using AddSource on the correct input.
    """
    rhino = get_rhino_connection()
    script = """
import Rhino
import Grasshopper as gh
import scriptcontext as sc
import System.Drawing
import System

# Get the active Grasshopper document
ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    # Find the number slider component
    slider = None
    for obj in ghdoc.Objects:
        if isinstance(obj, gh.Kernel.Special.GH_NumberSlider):
            slider = obj
            break
    if slider is None:
        print("Number slider component not found.")
    else:
        # Find the Populate 3D component
        populate3d = None
        for obj in ghdoc.Objects:
            if obj.Name == "Populate 3D":
                populate3d = obj
                break
        if populate3d is None:
            print("Populate 3D component not found.")
        else:
            # Connect the slider to the Populate 3D count input (second input)
            populate3d.Params.Input[1].AddSource(slider)
            print("Connected number slider to Populate 3D count input using AddSource.")
else:
    print("No Grasshopper document found")
"""
    print("Executing Grasshopper API script to connect slider to Populate 3D count input...")
    result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
    print("Script result:", result)

def force_grasshopper_recompute():
    """
    Force Grasshopper to recompute the solution and refresh the canvas.
    """
    rhino = get_rhino_connection()
    script = """
import Grasshopper as gh

ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    ghdoc.NewSolution(True)
    ghdoc.RequestCanvasUpdate()
    print("Forced Grasshopper to recompute and refresh the canvas.")
else:
    print("No Grasshopper document found")
"""
    print("Executing Grasshopper API script to force recompute and refresh...")
    result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
    print("Script result:", result)

if __name__ == "__main__":
    create_point_component_with_internalized_points()
    create_populate3d_component()
    test_grasshopper_api()
    connect_slider_to_populate3d()
    force_grasshopper_recompute()
