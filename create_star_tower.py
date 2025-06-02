from rhinomcp.tools.create_object import create_object
from mcp.server.fastmcp import Context
import math

def create_star_tower():
    # Create points for a 5-pointed star
    points = []
    radius = 20
    num_points = 5
    
    # Generate points for the star base
    for i in range(num_points * 2):
        angle = i * math.pi / num_points
        r = radius if i % 2 == 0 else radius * 0.4
        x = r * math.cos(angle)
        y = r * math.sin(angle)
        points.append([x, y, 0])
    
    # Create the top points with a twist
    top_points = []
    for i, p in enumerate(points):
        # Add twist by rotating the top points
        twist_angle = math.pi / 2  # 90-degree twist
        x = p[0] * math.cos(twist_angle) - p[1] * math.sin(twist_angle)
        y = p[0] * math.sin(twist_angle) + p[1] * math.cos(twist_angle)
        top_points.append([x, y, 100])
    
    # Create the surface
    result = create_object(
        Context(),
        type='SURFACE',
        params={
            'count': (10, 2),  # 10 points in U direction, 2 points in V direction
            'points': points + top_points,
            'degree': (3, 1)
        }
    )
    print(result)

if __name__ == "__main__":
    create_star_tower() 