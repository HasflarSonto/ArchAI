from rhinomcp import get_rhino_connection
import math

def create_box_array():
    # Get the Rhino connection
    rhino = get_rhino_connection()
    
    # Base size for the boxes
    base_size = 10
    spacing = 15  # Space between boxes
    
    # Create a 6x6x6 grid
    for i in range(6):
        for j in range(6):
            for k in range(6):
                # Calculate position
                x = (i - 2.5) * spacing  # Center the grid around origin
                y = (j - 2.5) * spacing
                z = (k - 2.5) * spacing
                
                # Calculate distance from origin
                distance = math.sqrt(x*x + y*y + z*z)
                
                # Calculate size based on distance (smaller as distance increases)
                size_factor = 1.0 - (distance / (spacing * 5))  # Normalize to 0-1 range
                size = base_size * size_factor
                
                # Calculate color based on distance (blue to red gradient)
                # Normalize distance to 0-1 range for color
                color_factor = distance / (spacing * 5)
                r = int(255 * color_factor)  # More red as distance increases
                g = 0
                b = int(255 * (1 - color_factor))  # More blue as distance decreases
                
                # Create the box
                result = rhino.send_command("create_object", {
                    "type": "BOX",
                    "params": {
                        "width": size,
                        "length": size,
                        "height": size
                    },
                    "translation": [x, y, z],
                    "color": [r, g, b]
                })
                
                print(f"Created box at ({x}, {y}, {z}) with size {size} and color ({r}, {g}, {b})")

if __name__ == "__main__":
    create_box_array() 