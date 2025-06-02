from typing import Dict, List, Any, Optional, Union
from rhinomcp import get_rhino_connection
import uuid
import json

class GrasshopperCanvas:
    """
    A Python interface for creating and manipulating Grasshopper definitions.
    This class provides a clean, Pythonic way to work with Grasshopper components.
    """
    
    def __init__(self):
        """Initialize a new Grasshopper canvas interface."""
        self.rhino = get_rhino_connection()
        self._components: Dict[str, Any] = {}
        self._id_map: Dict[str, str] = {}
        
    def _execute_script(self, script: str) -> Dict[str, Any]:
        """Execute a RhinoScript and return the result."""
        result = self.rhino.send_command("execute_rhinoscript_python_code", {"code": script})
        if isinstance(result, dict) and 'result' in result:
            result_str = result['result']
            if 'Print output:' in result_str:
                json_str = result_str.split('Print output:')[1].strip()
                try:
                    return json.loads(json_str)
                except json.JSONDecodeError:
                    return {"error": "Failed to parse script output"}
        return {"error": "Failed to execute script"}
    
    def _create_component(self, component_type: str, position: List[float], **kwargs) -> str:
        """Create a new component of the specified type."""
        semantic_id = f"{component_type.lower()}_{str(uuid.uuid4())[:8]}"
        
        script = f"""
import Rhino
import Grasshopper as gh
import scriptcontext as sc
import System.Drawing

# Get the active Grasshopper document
ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    # Create a new {component_type} component
    component = gh.Kernel.GH_Component.CreateComponent(
        "{component_type}",  # Component name
        "{component_type}",  # Nickname
        "A {component_type} component",  # Description
        "Params",  # Category
        "Primitive",  # Subcategory
        gh.Kernel.GH_Exposure.primary,  # Exposure
        gh.Kernel.GH_Exposure.primary  # Exposure
    )
    
    if component:
        # Set the component's position
        component.Attributes.Pivot = System.Drawing.PointF({position[0]}, {position[1]})
        
        # Add the component to the document
        ghdoc.AddObject(component, False)
        
        # Set component-specific properties
        {self._get_property_setter_script(component_type, kwargs)}
        
        print(json.dumps({{
            "status": "success",
            "component_id": str(component.InstanceGuid)
        }}))
    else:
        print(json.dumps({{
            "status": "error",
            "message": "Failed to create {component_type} component"
        }}))
else:
    print(json.dumps({{
        "status": "error",
        "message": "No Grasshopper document found"
    }}))
"""
        result = self._execute_script(script)
        if result.get("status") == "success":
            self._id_map[semantic_id] = result["component_id"]
            return semantic_id
        raise Exception(f"Failed to create {component_type} component: {result.get('message')}")
    
    def _get_property_setter_script(self, component_type: str, properties: Dict[str, Any]) -> str:
        """Generate script to set component-specific properties."""
        script_lines = []
        
        if component_type == "NumberSlider":
            if "min" in properties:
                script_lines.append(f"component.Slider.Minimum = {properties['min']}")
            if "max" in properties:
                script_lines.append(f"component.Slider.Maximum = {properties['max']}")
            if "value" in properties:
                script_lines.append(f"component.Slider.Value = {properties['value']}")
        
        elif component_type == "Panel":
            if "text" in properties:
                script_lines.append(f"component.PanelContent = '{properties['text']}'")
        
        elif component_type == "Point":
            if "value" in properties:
                x, y, z = properties["value"]
                script_lines.append(f"point = Rhino.Geometry.Point3d({x}, {y}, {z})")
                script_lines.append("component.PersistentData.Append(gh.Kernel.Types.GH_Point(point))")
        
        return "\n        ".join(script_lines)
    
    def add_point(self, value: List[float], position: List[float]) -> str:
        """Add a point component with the specified value and position."""
        return self._create_component("Point", position, value=value)
    
    def add_points(self, values: List[List[float]], position: List[float]) -> str:
        """Add a point component with multiple values."""
        return self._create_component("Point", position, value=values)
    
    def add_panel(self, text: str, position: List[float]) -> str:
        """Add a panel component with the specified text."""
        return self._create_component("Panel", position, text=text)
    
    def add_slider(self, min: float, max: float, value: float, position: List[float]) -> str:
        """Add a number slider component with the specified range and value."""
        return self._create_component("NumberSlider", position, min=min, max=max, value=value)
    
    def connect(self, source_id: str, source_port: str, target_id: str, target_port: str,
                graft: bool = False, flatten: bool = False, simplify: bool = False) -> None:
        """Connect two components with optional data handling."""
        script = f"""
import Rhino
import Grasshopper as gh
import scriptcontext as sc

# Get the active Grasshopper document
ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    # Find source and target components
    source = None
    target = None
    for obj in ghdoc.Objects:
        if str(obj.InstanceGuid) == "{self._id_map[source_id]}":
            source = obj
        elif str(obj.InstanceGuid) == "{self._id_map[target_id]}":
            target = obj
    
    if source and target:
        # Get the source output and target input
        source_output = None
        target_input = None
        
        if hasattr(source, "Params"):
            for param in source.Params.Output:
                if param.Name == "{source_port}":
                    source_output = param
                    break
        
        if hasattr(target, "Params"):
            for param in target.Params.Input:
                if param.Name == "{target_port}":
                    target_input = param
                    break
        
        if source_output and target_input:
            # Set data handling options
            if {graft}:
                target_input.Access = gh.Kernel.GH_ParamAccess.tree
            if {flatten}:
                target_input.Access = gh.Kernel.GH_ParamAccess.list
            if {simplify}:
                target_input.Simplify = True
            
            # Create the connection
            target_input.AddSource(source_output)
            print(json.dumps({{"status": "success"}}))
        else:
            print(json.dumps({{"status": "error", "message": "Could not find specified ports"}}))
    else:
        print(json.dumps({{"status": "error", "message": "Could not find specified components"}}))
else:
    print(json.dumps({{"status": "error", "message": "No Grasshopper document found"}}))
"""
        result = self._execute_script(script)
        if result.get("status") != "success":
            raise Exception(f"Failed to connect components: {result.get('message')}")
    
    def get_canvas_info(self) -> Dict[str, Any]:
        """Get information about the current canvas state."""
        script = """
import Rhino
import Grasshopper as gh
import scriptcontext as sc
import json

# Get the active Grasshopper document
ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    canvas_info = {
        "components": {},
        "id_map": {}
    }
    
    for component in ghdoc.Objects:
        try:
            semantic_id = f"{component.GetType().Name.lower()}_{str(component.InstanceGuid)[:8]}"
            info = {
                "type": component.GetType().Name,
                "position": [float(component.Attributes.Pivot.X), float(component.Attributes.Pivot.Y)],
                "inputs": {},
                "outputs": {}
            }
            
            if hasattr(component, "Params"):
                # Process inputs
                for param in component.Params.Input:
                    input_info = {
                        "data_handling": [],
                        "connections": []
                    }
                    
                    if hasattr(param, "DataMapping"):
                        mapping_map = {1: "flatten", 2: "graft", 3: "none"}
                        if param.DataMapping in mapping_map:
                            input_info["data_handling"].append(mapping_map[param.DataMapping])
                    
                    if hasattr(param, "Simplify") and param.Simplify:
                        input_info["data_handling"].append("simplify")
                    if hasattr(param, "Reverse") and param.Reverse:
                        input_info["data_handling"].append("reverse")
                    if hasattr(param, "Graft") and param.Graft:
                        input_info["data_handling"].append("graft")
                    
                    if hasattr(param, "Sources"):
                        for source in param.Sources:
                            if source:
                                source_comp = source.Attributes.GetTopLevel.DocObject
                                source_id = f"{source_comp.GetType().Name.lower()}_{str(source_comp.InstanceGuid)[:8]}"
                                input_info["connections"].append(f"{source_id}:{source.Name}")
                    
                    info["inputs"][param.Name] = input_info
                
                # Process outputs
                for param in component.Params.Output:
                    output_info = {
                        "data_handling": [],
                        "connections": []
                    }
                    
                    if hasattr(param, "DataMapping"):
                        mapping_map = {1: "flatten", 2: "graft", 3: "none"}
                        if param.DataMapping in mapping_map:
                            output_info["data_handling"].append(mapping_map[param.DataMapping])
                    
                    if hasattr(param, "Simplify") and param.Simplify:
                        output_info["data_handling"].append("simplify")
                    if hasattr(param, "Reverse") and param.Reverse:
                        output_info["data_handling"].append("reverse")
                    if hasattr(param, "Graft") and param.Graft:
                        output_info["data_handling"].append("graft")
                    
                    if hasattr(param, "Recipients"):
                        for recipient in param.Recipients:
                            if recipient:
                                recipient_comp = recipient.Attributes.GetTopLevel.DocObject
                                recipient_id = f"{recipient_comp.GetType().Name.lower()}_{str(recipient_comp.InstanceGuid)[:8]}"
                                output_info["connections"].append(f"{recipient_id}:{param.Name}")
                    
                    info["outputs"][param.Name] = output_info
            
            canvas_info["components"][semantic_id] = info
            canvas_info["id_map"][semantic_id] = str(component.InstanceGuid)
        except Exception as e:
            print(f"Error processing component: {str(e)}")
    
    print(json.dumps(canvas_info))
else:
    print(json.dumps({"error": "No Grasshopper document found"}))
"""
        return self._execute_script(script)
    
    def clear(self) -> None:
        """Clear all components from the canvas."""
        script = """
import Grasshopper as gh
import json

ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    ghdoc.Objects.Clear()
    print(json.dumps({"status": "success"}))
else:
    print(json.dumps({"status": "error", "message": "No Grasshopper document found"}))
"""
        result = self._execute_script(script)
        if result.get("status") != "success":
            error_msg = result.get("message", "Unknown error")
            raise Exception(f"Failed to clear canvas: {error_msg}")
        self._components.clear()
        self._id_map.clear() 