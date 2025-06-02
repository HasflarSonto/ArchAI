from rhinomcp import get_rhino_connection
import math
import random

def fibonacci_spiral(num_points):
    """
    Generate points for a Fibonacci spiral pattern.
    Uses the golden angle (137.5 degrees) for natural-looking distribution.
    """
    points = []
    a, b = 0, 1  # Initialize Fibonacci sequence
    
    for i in range(num_points):
        # Calculate next Fibonacci number
        c = a + b
        a, b = b, c
        
        # Calculate angle and radius for spiral
        angle = i * 137.5 * (math.pi/180)  # Golden angle in radians
        radius = math.sqrt(i) * 2  # Scale radius with square root of index
        
        # Calculate 3D position
        x = radius * math.cos(angle)
        y = radius * math.sin(angle)
        z = i * 0.5  # Add some height variation
        
        points.append([x, y, z])
    
    return points

def create_spiral_boxes():
    """
    Create a Fibonacci spiral pattern of boxes in Rhino.
    Boxes get smaller and are randomly rotated as they go up.
    """
    # Get the Rhino connection
    rhino = get_rhino_connection()
    
    # Parameters
    num_boxes = 30
    base_size = 2.0
    size_reduction = 0.7  # How much the size reduces over the height
    
    # Generate spiral points
    spiral_points = fibonacci_spiral(num_boxes)
    
    # Create boxes at each point
    for i, point in enumerate(spiral_points):
        # Calculate box size (decreases as we go up)
        size = base_size * (1 - (i/len(spiral_points)) * size_reduction)
        
        # Calculate color (gradient from blue to red based on height)
        height_factor = i / len(spiral_points)
        r = int(255 * height_factor)
        g = 0
        b = int(255 * (1 - height_factor))
        
        # Random rotation angle
        rotation_angle = random.uniform(0, 360)
        
        # Create the box
        result = rhino.send_command("create_object", {
            "type": "BOX",
            "params": {
                "width": size,
                "length": size,
                "height": size
            },
            "translation": point,
            "rotation": [0, 0, rotation_angle],
            "color": [r, g, b]
        })
        
        print(f"Created box {i+1}/{num_boxes} at {point} with size {size:.2f}")

if __name__ == "__main__":
    create_spiral_boxes() 