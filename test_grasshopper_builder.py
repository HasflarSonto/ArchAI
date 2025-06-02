from grasshopper_builder import GrasshopperBuilder
from component_registry import ComponentRegistry

def test_voronoi_3d():
    # Initialize component registry
    registry = ComponentRegistry()
    
    # Search for components
    populate_results = registry.search_components("Populate 3D points in space")
    voronoi_results = registry.search_components("Create 3D Voronoi diagram")
    
    if not populate_results or not voronoi_results:
        print("Could not find required components")
        return
    
    # Get the best matching components
    populate_comp = populate_results[0]
    voronoi_comp = voronoi_results[0]
    
    print(f"Using Populate 3D component: {populate_comp['name']}")
    print(f"Using Voronoi component: {voronoi_comp['name']}")
    
    # Create a new builder instance
    builder = GrasshopperBuilder()
    
    # Add components using their GUIDs
    populate_id = builder.add_component_by_guid(populate_comp['guid'], [100, 100])
    voronoi_id = builder.add_component_by_guid(voronoi_comp['guid'], [300, 100])
    
    # Connect components based on their input/output types
    # Find matching input/output pairs
    for out in populate_comp['outputs']:
        for inp in voronoi_comp['inputs']:
            if out['type'] == inp['type']:
                builder.connect(populate_id, out['name'], voronoi_id, inp['name'])
                print(f"Connected {out['name']} to {inp['name']}")
    
    # Run the builder to create the components in Grasshopper
    builder.run()

if __name__ == "__main__":
    test_voronoi_3d() 