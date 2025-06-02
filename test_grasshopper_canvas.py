from grasshopper_canvas import GrasshopperCanvas
import sys

def test_simple_workflow():
    """Test a simple workflow creating a point and connecting it to a circle."""
    try:
        canvas = GrasshopperCanvas()
        
        # Create components
        point = canvas.add_point([0, 0, 0], position=[100, 100])
        circle = canvas.add_circle(position=[150, 100])
        
        # Connect components
        canvas.connect(point, "Point", circle, "Center")
        
        # Get canvas info
        info = canvas.get_canvas_info()
        print("\nCanvas Summary:")
        print("=" * 50)
        print(f"Found {len(info['components'])} components")
        
        # Print details for each component
        for comp_id, comp in info["components"].items():
            print(f"\nComponent: {comp_id}")
            print(f"Type: {comp['type']}")
            print(f"Position: {comp['position']}")
            
            if comp["inputs"]:
                print("\nInputs:")
                for name, inp in comp["inputs"].items():
                    print(f"  {name}:")
                    if inp["data_handling"]:
                        print(f"    Data Handling: {', '.join(inp['data_handling'])}")
                    if inp["connections"]:
                        print("    Connections:")
                        for conn in inp["connections"]:
                            print(f"      {conn}")
            
            if comp["outputs"]:
                print("\nOutputs:")
                for name, out in comp["outputs"].items():
                    print(f"  {name}:")
                    if out["data_handling"]:
                        print(f"    Data Handling: {', '.join(out['data_handling'])}")
                    if out["connections"]:
                        print("    Connections:")
                        for conn in out["connections"]:
                            print(f"      {conn}")
            
            print("-" * 50)
    except Exception as e:
        print(f"Error in simple workflow: {e}")
        raise

def test_complex_workflow():
    """Test a more complex workflow with multiple components and data handling."""
    try:
        canvas = GrasshopperCanvas()
        
        # Create a grid of points
        points = []
        for x in range(0, 50, 10):
            for y in range(0, 50, 10):
                point = canvas.add_point([x, y, 0], position=[100 + x/2, 100 + y/2])
                points.append(point)
        
        # Create a surface
        surface = canvas.add_surface(position=[200, 100])
        
        # Connect points to surface with data handling
        for point in points:
            canvas.connect(point, "Point", surface, "Points", 
                          data_handling=["graft", "simplify"])
        
        # Get canvas info
        info = canvas.get_canvas_info()
        print("\nComplex Workflow Summary:")
        print("=" * 50)
        print(f"Found {len(info['components'])} components")
        
        # Print details for each component
        for comp_id, comp in info["components"].items():
            print(f"\nComponent: {comp_id}")
            print(f"Type: {comp['type']}")
            print(f"Position: {comp['position']}")
            
            if comp["inputs"]:
                print("\nInputs:")
                for name, inp in comp["inputs"].items():
                    print(f"  {name}:")
                    if inp["data_handling"]:
                        print(f"    Data Handling: {', '.join(inp['data_handling'])}")
                    if inp["connections"]:
                        print("    Connections:")
                        for conn in inp["connections"]:
                            print(f"      {conn}")
            
            if comp["outputs"]:
                print("\nOutputs:")
                for name, out in comp["outputs"].items():
                    print(f"  {name}:")
                    if out["data_handling"]:
                        print(f"    Data Handling: {', '.join(out['data_handling'])}")
                    if out["connections"]:
                        print("    Connections:")
                        for conn in out["connections"]:
                            print(f"      {conn}")
            
            print("-" * 50)
    except Exception as e:
        print(f"Error in complex workflow: {e}")
        raise

if __name__ == "__main__":
    try:
        # Clear any existing components
        canvas = GrasshopperCanvas()
        canvas.clear()
        
        # Run tests
        print("Testing simple workflow...")
        test_simple_workflow()
        
        print("\nTesting complex workflow...")
        test_complex_workflow()
    except Exception as e:
        print(f"\nError: {e}")
        print("\nMake sure Grasshopper is open and a document is active.")
        sys.exit(1) 