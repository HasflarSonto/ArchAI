from rhinomcp import get_rhino_connection
import math

def create_star_skyscraper():
    rhino = get_rhino_connection()

    outer_radius = 50
    inner_radius = 25
    num_points = 5
    num_floors = 30
    floor_height = 6
    rotation_per_floor = 10  # degrees per floor

    star_curve_ids = []
    for floor in range(num_floors):
        z = floor * floor_height
        rotation = math.radians(floor * rotation_per_floor)
        points = []
        for i in range(num_points * 2):
            angle = i * (2 * math.pi / (num_points * 2)) + rotation
            radius = outer_radius if i % 2 == 0 else inner_radius
            x = radius * math.cos(angle)
            y = radius * math.sin(angle)
            points.append([x, y, z])
        points.append(points[0])  # close the star
        # Create the polyline for this floor
        result = rhino.send_command("create_object", {
            "type": "POLYLINE",
            "params": {
                "points": points
            }
        })
        # The result may be a dict or a list of dicts
        if isinstance(result, dict) and "id" in result:
            star_curve_ids.append(result["id"])
        elif isinstance(result, list) and len(result) > 0 and "id" in result[0]:
            star_curve_ids.append(result[0]["id"])

    # Loft through all the star curves
    loft_result = rhino.send_command("add_loft_srf", {
        "object_ids": star_curve_ids,
        "loft_type": 0,  # Normal
        "simplify_method": 0,
        "closed": False
    })

    print("Created twisting star-shaped skyscraper!")

if __name__ == "__main__":
    create_star_skyscraper() 