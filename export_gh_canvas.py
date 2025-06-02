from rhinomcp import get_rhino_connection
import json
from typing import Dict, List, Any
import uuid
import logging

# Configure logging to only show errors
logging.getLogger('RhinoMCPServer').setLevel(logging.ERROR)

def get_all_attrs(obj):
    result = {}
    for attr in dir(obj):
        if not attr.startswith('__'):
            try:
                val = getattr(obj, attr)
                # Avoid dumping huge objects recursively
                if callable(val):
                    continue
                # Try to serialize simple types, otherwise just str
                if isinstance(val, (int, float, str, bool, list, dict, tuple)):
                    result[attr] = val
                else:
                    result[attr] = str(val)
            except Exception as e:
                result[attr] = f"<error: {e}>"
    return result

def get_component_info(component) -> Dict[str, Any]:
    """Extract relevant information from a Grasshopper component using a generalized approach."""
    semantic_id = "{0}_{1}".format(
        component.GetType().Name.lower(),
        str(uuid.uuid4())[:8]
    )
    
    info = {
        "type": component.GetType().Name,
        "position": [float(component.Attributes.Pivot.X), float(component.Attributes.Pivot.Y)],
        "value": None,
        "inputs": {},
        "outputs": {},
        "data_handling": []
    }

    # Extract data handling modifiers
    if hasattr(component, "Params"):
        for param in component.Params.Input:
            if hasattr(param, "DataMapping"):
                mapping_map = {
                    1: "flatten",
                    2: "graft",
                    3: "none"
                }
                if param.DataMapping in mapping_map:
                    info["data_handling"].append(mapping_map[param.DataMapping])
            
            if hasattr(param, "Simplify") and param.Simplify:
                info["data_handling"].append("simplify")
            if hasattr(param, "Reverse") and param.Reverse:
                info["data_handling"].append("reverse")
            if hasattr(param, "Graft") and param.Graft:
                info["data_handling"].append("graft")

    # Extract values from PersistentData and VolatileData
    def extract_value(data):
        if not data:
            return None
        
        values = []
        for item in data:
            if hasattr(item, 'Value'):
                val = item.Value
                if hasattr(val, "X") and hasattr(val, "Y"):
                    z = float(val.Z) if hasattr(val, "Z") else 0.0
                    values.append({
                        "Coordinate": {
                            "X": float(val.X),
                            "Y": float(val.Y),
                            "Z": z
                        }
                    })
                elif hasattr(val, "Start") and hasattr(val, "End"):
                    values.append({
                        "Range": {
                            "Start": float(val.Start),
                            "End": float(val.End)
                        }
                    })
                else:
                    try:
                        values.append(float(val))
                    except (ValueError, TypeError):
                        values.append(str(val))
        return values[0] if len(values) == 1 else values

    # Try to get value from PersistentData
    if hasattr(component, "PersistentData"):
        info["value"] = extract_value(list(component.PersistentData.AllData(True)))
    
    # If no value in PersistentData, try VolatileData
    if info["value"] is None and hasattr(component, "VolatileData"):
        info["value"] = extract_value(list(component.VolatileData.AllData(True)))

    # Handle parameters and connections
    if hasattr(component, "Params"):
        # Process inputs
        for param in component.Params.Input:
            input_info = {
                "data_handling": [],
                "connections": []
            }
            
            # Extract data handling modifiers
            if hasattr(param, "DataMapping"):
                mapping_map = {
                    1: "flatten",
                    2: "graft",
                    3: "none"
                }
                if param.DataMapping in mapping_map:
                    input_info["data_handling"].append(mapping_map[param.DataMapping])
            
            if hasattr(param, "Simplify") and param.Simplify:
                input_info["data_handling"].append("simplify")
            if hasattr(param, "Reverse") and param.Reverse:
                input_info["data_handling"].append("reverse")
            if hasattr(param, "Graft") and param.Graft:
                input_info["data_handling"].append("graft")
            
            # Extract connections
            if hasattr(param, "Sources"):
                for source in param.Sources:
                    if source:
                        source_comp = source.Attributes.GetTopLevel.DocObject
                        source_id = "{0}_{1}".format(
                            source_comp.GetType().Name.lower(),
                            source_comp.InstanceGuid.ToString()[:8]
                        )
                        input_info["connections"].append(f"{source_id}:{source.Name}")
            
            info["inputs"][param.Name] = input_info
        
        # Process outputs
        for param in component.Params.Output:
            output_info = {
                "data_handling": [],
                "connections": [],
                "value": None
            }
            
            # Extract output value
            if hasattr(param, "PersistentData"):
                output_info["value"] = extract_value(list(param.PersistentData.AllData(True)))
            if output_info["value"] is None and hasattr(param, "VolatileData"):
                output_info["value"] = extract_value(list(param.VolatileData.AllData(True)))
            
            # Extract data handling modifiers
            if hasattr(param, "DataMapping"):
                mapping_map = {
                    1: "flatten",
                    2: "graft",
                    3: "none"
                }
                if param.DataMapping in mapping_map:
                    output_info["data_handling"].append(mapping_map[param.DataMapping])
            
            if hasattr(param, "Simplify") and param.Simplify:
                output_info["data_handling"].append("simplify")
            if hasattr(param, "Reverse") and param.Reverse:
                output_info["data_handling"].append("reverse")
            if hasattr(param, "Graft") and param.Graft:
                output_info["data_handling"].append("graft")
            
            # Extract connections
            if hasattr(param, "Recipients"):
                for recipient in param.Recipients:
                    if recipient:
                        recipient_comp = recipient.Attributes.GetTopLevel.DocObject
                        recipient_id = "{0}_{1}".format(
                            recipient_comp.GetType().Name.lower(),
                            recipient_comp.InstanceGuid.ToString()[:8]
                        )
                        output_info["connections"].append(f"{recipient_id}:{param.Name}")
            
            info["outputs"][param.Name] = output_info
    
    return semantic_id, info

def export_grasshopper_canvas() -> Dict[str, Any]:
    """
    Export the current Grasshopper canvas information in a structured format.
    Returns a dictionary containing all components and their connections.
    """
    rhino = get_rhino_connection()
    
    script = """
import Grasshopper as gh
import json
import System
import uuid

def get_all_attrs(obj):
    result = {}
    for attr in dir(obj):
        if not attr.startswith('__'):
            try:
                val = getattr(obj, attr)
                if callable(val):
                    continue
                if isinstance(val, (int, float, str, bool, list, dict, tuple)):
                    result[attr] = val
                else:
                    result[attr] = str(val)
            except Exception as e:
                result[attr] = '<error: %s>' % e
    return result

def get_component_info(component):
    semantic_id = "{0}_{1}".format(
        component.GetType().Name.lower(),
        str(uuid.uuid4())[:8]
    )
    info = {
        "type": component.GetType().Name,
        "position": [float(component.Attributes.Pivot.X), float(component.Attributes.Pivot.Y)],
        "all_attrs": get_all_attrs(component),
        "inputs": {},
        "outputs": {},
    }
    if hasattr(component, "Params"):
        info["inputs"] = {}
        for param in getattr(component.Params, "Input", []):
            info["inputs"][param.Name] = {
                "all_attrs": get_all_attrs(param)
            }
        info["outputs"] = {}
        for param in getattr(component.Params, "Output", []):
            info["outputs"][param.Name] = {
                "all_attrs": get_all_attrs(param)
            }
    return semantic_id, info

# Get the active Grasshopper document
ghdoc = gh.Instances.ActiveCanvas.Document
if ghdoc:
    canvas_info = {
        "components": {},
        "id_map": {}
    }
    for component in ghdoc.Objects:
        try:
            semantic_id, info = get_component_info(component)
            canvas_info["components"][semantic_id] = info
            canvas_info["id_map"][semantic_id] = component.InstanceGuid.ToString()
        except Exception as e:
            print("Error extracting component: ", str(e))
    print(json.dumps(canvas_info))
else:
    print(json.dumps({"error": "No Grasshopper document found"}))
"""
    
    result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
    
    try:
        if isinstance(result, dict) and 'result' in result:
            result_str = result['result']
            if 'Print output:' in result_str:
                json_str = result_str.split('Print output:')[1].strip()
                return json.loads(json_str)
            else:
                return {"error": "Failed to parse canvas information"}
        else:
            return {"error": "Failed to parse canvas information"}
    except json.JSONDecodeError:
        return {"error": "Failed to parse canvas information"}

def print_canvas_summary(canvas_info: Dict[str, Any]):
    """Print a human-readable summary of the Grasshopper canvas."""
    if "error" in canvas_info:
        print(f"Error: {canvas_info['error']}")
        return
    
    print("\nGrasshopper Canvas Summary")
    print("=" * 50)
    print(f"Total Components: {len(canvas_info['components'])}")
    print("\nComponents:")
    
    for comp_id, comp in canvas_info["components"].items():
        print(f"\n{comp_id}")
        print(f"  Type: {comp['type']}")
        print(f"  Position: {comp['position']}")
        
        if comp["value"] is not None:
            print(f"  Value: {comp['value']}")
        
        if comp["inputs"]:
            print("  Inputs:")
            for name, inp in comp["inputs"].items():
                print(f"    - {name}")
                if inp["data_handling"]:
                    print(f"      Data Handling: {', '.join(inp['data_handling'])}")
                if inp["connections"]:
                    print("      Connections:")
                    for conn in inp["connections"]:
                        if isinstance(conn, dict):
                            if "source_id" in conn:
                                print(f"        From: {conn['source_id']} ({conn['source_name']})")
                            elif "recipient_id" in conn:
                                print(f"        To: {conn['recipient_id']} ({conn['recipient_name']})")
                        else:
                            print(f"        {conn}")
        
        if comp["outputs"]:
            print("  Outputs:")
            for name, out in comp["outputs"].items():
                print(f"    - {name}")
                if out["data_handling"]:
                    print(f"      Data Handling: {', '.join(out['data_handling'])}")
                if out["connections"]:
                    print("      Connections:")
                    for conn in out["connections"]:
                        if isinstance(conn, dict):
                            if "source_id" in conn:
                                print(f"        From: {conn['source_id']} ({conn['source_name']})")
                            elif "recipient_id" in conn:
                                print(f"        To: {conn['recipient_id']} ({conn['recipient_name']})")
                        else:
                            print(f"        {conn}")

if __name__ == "__main__":
    # Get the canvas information
    canvas_info = export_grasshopper_canvas()
    
    # Print a clean summary
    print("\nCanvas Summary:")
    print("=" * 50)
    
    if "error" in canvas_info:
        print(f"Error: {canvas_info['error']}")
    else:
        print(f"Found {len(canvas_info['components'])} components")
        
        # Print details for each component
        for comp_id, comp in canvas_info["components"].items():
            print(f"\nComponent: {comp_id}")
            print(f"Type: {comp['type']}")
            print(f"Position: {comp['position']}")
            
            # Extract value from all_attrs if it exists
            if "all_attrs" in comp:
                attrs = comp["all_attrs"]
                if "InstanceDescription" in attrs:
                    print(f"Description: {attrs['InstanceDescription']}")
                if "CurrentValue" in attrs:
                    print(f"Value: {attrs['CurrentValue']}")
            
            # Print inputs
            if "inputs" in comp and comp["inputs"]:
                print("\nInputs:")
                for name, inp in comp["inputs"].items():
                    print(f"  {name}:")
                    if "all_attrs" in inp:
                        attrs = inp["all_attrs"]
                        if "DataMapping" in attrs:
                            print(f"    Data Mapping: {attrs['DataMapping']}")
                        if "Simplify" in attrs:
                            print(f"    Simplify: {attrs['Simplify']}")
                        if "Reverse" in attrs:
                            print(f"    Reverse: {attrs['Reverse']}")
                        if "Sources" in attrs and attrs["Sources"]:
                            print(f"    Sources: {attrs['Sources']}")
            
            # Print outputs
            if "outputs" in comp and comp["outputs"]:
                print("\nOutputs:")
                for name, out in comp["outputs"].items():
                    print(f"  {name}:")
                    if "all_attrs" in out:
                        attrs = out["all_attrs"]
                        if "InstanceDescription" in attrs:
                            print(f"    Description: {attrs['InstanceDescription']}")
                        if "DataMapping" in attrs:
                            print(f"    Data Mapping: {attrs['DataMapping']}")
                        if "Simplify" in attrs:
                            print(f"    Simplify: {attrs['Simplify']}")
                        if "Reverse" in attrs:
                            print(f"    Reverse: {attrs['Reverse']}")
                        if "Recipients" in attrs and attrs["Recipients"]:
                            print(f"    Recipients: {attrs['Recipients']}")
            
            print("-" * 50) 