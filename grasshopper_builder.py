from typing import List, Dict, Any, Optional, Union
import uuid

class GrasshopperBuilder:
    """
    High-level builder for Grasshopper definitions.
    Simplifies the creation and connection of components.
    """
    def __init__(self):
        self.components: List[Dict[str, Any]] = []
        self.connections: List[Dict[str, Any]] = []
        self._id_map: Dict[str, str] = {}  # user_id -> internal id

    def add_slider(self, min: float, max: float, value: float, position: List[float]) -> str:
        """Add a number slider component."""
        comp_id = f"slider_{uuid.uuid4().hex[:8]}"
        self.components.append({
            "type": "slider",
            "id": comp_id,
            "min": min,
            "max": max,
            "value": value,
            "position": position
        })
        return comp_id

    def add_point(self, coords: List[float], position: List[float]) -> str:
        """Add a point component."""
        comp_id = f"point_{uuid.uuid4().hex[:8]}"
        self.components.append({
            "type": "point",
            "id": comp_id,
            "coords": coords,
            "position": position
        })
        return comp_id

    def add_panel(self, text: str, position: List[float]) -> str:
        """Add a panel component."""
        comp_id = f"panel_{uuid.uuid4().hex[:8]}"
        self.components.append({
            "type": "panel",
            "id": comp_id,
            "text": text,
            "position": position
        })
        return comp_id

    def add_component(self, type_name: str, position: List[float], **kwargs) -> str:
        """Add a generic component by type name (or GUID in the future)."""
        comp_id = f"{type_name.lower()}_{uuid.uuid4().hex[:8]}"
        comp = {"type": type_name, "id": comp_id, "position": position}
        comp.update(kwargs)
        self.components.append(comp)
        return comp_id

    def add_component_by_guid(self, guid: str, position: List[float], **kwargs) -> str:
        """Add a generic component by its GUID."""
        comp_id = f"comp_{uuid.uuid4().hex[:8]}"
        comp = {"type": "generic", "id": comp_id, "guid": guid, "position": position}
        comp.update(kwargs)
        self.components.append(comp)
        return comp_id

    def connect(self, source: str, source_output: str, target: str, target_input: str, **data_handling) -> None:
        """Connect two components by their ids and port names."""
        self.connections.append({
            "source": source,
            "source_output": source_output,
            "target": target,
            "target_input": target_input,
            "data_handling": data_handling
        })

    def run(self):
        """Build and execute the Grasshopper definition in Rhino/Grasshopper."""
        from rhinomcp import get_rhino_connection
        rhino = get_rhino_connection()
        script_lines = [
            "import Rhino",
            "import Grasshopper as gh",
            "import scriptcontext as sc",
            "import System.Drawing",
            "",
            "# Get the active Grasshopper document",
            "ghdoc = gh.Instances.ActiveCanvas.Document",
            "if ghdoc:",
            "    # Create components based on builder state",
            "    for comp in " + repr(self.components) + ":",
            "        if comp['type'] == 'panel':",
            "            panel = gh.Kernel.Special.GH_Panel()",
            "            panel.CreateAttributes()",
            "            panel.Attributes.Pivot = System.Drawing.PointF(comp['position'][0], comp['position'][1])",
            "            panel.UserText = comp['text']",
            "            ghdoc.AddObject(panel, False)",
            "        elif comp['type'] == 'slider':",
            "            slider = gh.Kernel.Special.GH_NumberSlider()",
            "            slider.CreateAttributes()",
            "            slider.Attributes.Pivot = System.Drawing.PointF(comp['position'][0], comp['position'][1])",
            "            slider.Slider.Minimum = comp['min']",
            "            slider.Slider.Maximum = comp['max']",
            "            slider.Slider.Value = comp['value']",
            "            ghdoc.AddObject(slider, False)",
            "        elif comp['type'] == 'point':",
            "            point = gh.Kernel.Types.GH_Point(Rhino.Geometry.Point3d(comp['coords'][0], comp['coords'][1], comp['coords'][2]))",
            "            ghdoc.AddObject(point, False)",
            "        elif comp['type'] == 'generic':",
            "            proxy = None",
            "            for obj in gh.Instances.ComponentServer.ObjectProxies:",
            "                if str(getattr(obj, 'Guid', '')) == comp['guid']:",
            "                    proxy = obj",
            "                    break",
            "            if proxy is None:",
            "                print('Component not found by GUID: ' + comp['guid'])",
            "            else:",
            "                component = proxy.CreateInstance()",
            "                component.CreateAttributes()",
            "                component.Attributes.Pivot = System.Drawing.PointF(comp['position'][0], comp['position'][1])",
            "                ghdoc.AddObject(component, False)",
            "        else:",
            "            print('Unsupported component type: ' + comp['type'])",
            "    # Force update",
            "    ghdoc.NewSolution(True)",
            "    print('Grasshopper definition executed successfully')",
            "else:",
            "    print('No Grasshopper document found')"
        ]
        script = "\n".join(script_lines)
        result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
        print("Script result:", result) 