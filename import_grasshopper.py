from rhinomcp import get_rhino_connection
import json

def import_definition():
    """
    Import a Grasshopper definition from a JSON file.
    """
    # Get the Rhino connection
    rhino = get_rhino_connection()
    
    # Read the JSON file
    with open('grasshopper_definition.json', 'r') as f:
        definition = json.load(f)
    
    # Import the definition
    result = rhino.send_command("import_grasshopper_definition", {
        "definition": definition
    })
    
    print("Grasshopper definition imported successfully!")
    print("Created a parametric pattern with:")
    print("- 10x10 grid")
    print("- Distance-based spheres")
    print("- Adjustable radius slider")

if __name__ == "__main__":
    import_definition() 