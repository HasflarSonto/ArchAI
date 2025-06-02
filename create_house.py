from rhinomcp import get_rhino_connection
import math

def create_house():
    """
    Create a simple house in Rhino with walls, roof, door, and windows.
    """
    # Get the Rhino connection
    rhino = get_rhino_connection()
    
    # House dimensions
    width = 10
    length = 12
    wall_height = 8
    roof_height = 4
    
    # Create main walls
    walls = rhino.send_command("create_object", {
        "type": "BOX",
        "params": {
            "width": width,
            "length": length,
            "height": wall_height
        },
        "translation": [0, 0, wall_height/2],
        "color": [200, 200, 200]  # Light gray
    })
    
    # Create roof using two triangular prisms
    # Front roof section
    front_roof = rhino.send_command("create_object", {
        "type": "BOX",
        "params": {
            "width": width/2,
            "length": length,
            "height": roof_height
        },
        "translation": [-width/4, 0, wall_height + roof_height/2],
        "rotation": [0, 45, 0],
        "color": [139, 69, 19]  # Brown
    })
    
    # Back roof section
    back_roof = rhino.send_command("create_object", {
        "type": "BOX",
        "params": {
            "width": width/2,
            "length": length,
            "height": roof_height
        },
        "translation": [width/4, 0, wall_height + roof_height/2],
        "rotation": [0, -45, 0],
        "color": [139, 69, 19]  # Brown
    })
    
    # Create door
    door = rhino.send_command("create_object", {
        "type": "BOX",
        "params": {
            "width": 3,
            "length": 0.5,
            "height": 6
        },
        "translation": [0, -length/2 - 0.25, 3],
        "color": [101, 67, 33]  # Dark brown
    })
    
    # Create windows
    window_size = 2
    window_positions = [
        [-width/3, -length/2 - 0.25, 5],  # Front left
        [width/3, -length/2 - 0.25, 5],   # Front right
        [-width/3, length/2 + 0.25, 5],   # Back left
        [width/3, length/2 + 0.25, 5]     # Back right
    ]
    
    for pos in window_positions:
        window = rhino.send_command("create_object", {
            "type": "BOX",
            "params": {
                "width": window_size,
                "length": 0.5,
                "height": window_size
            },
            "translation": pos,
            "color": [135, 206, 235]  # Sky blue
        })
    
    print("House created successfully!")
    print(f"Dimensions: {width}x{length}x{wall_height} with {roof_height} roof height")

if __name__ == "__main__":
    create_house() 