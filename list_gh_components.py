from rhinomcp import get_rhino_connection

def list_grasshopper_components():
    """
    List all available Grasshopper component names and their GUIDs.
    """
    rhino = get_rhino_connection()
    script = """
import Grasshopper as gh

def safe_get(obj, attrs):
    for attr in attrs:
        if hasattr(obj, attr):
            return getattr(obj, attr)
    return str(obj)

print('Available Grasshopper components:')
for obj in gh.Instances.ComponentServer.ObjectProxies:
    name = safe_get(obj, ['Name', 'ComponentName'])
    desc = safe_get(obj, ['Description'])
    guid = safe_get(obj, ['Guid'])
    # Try to get more readable info from Desc
    if hasattr(obj, 'Desc') and obj.Desc:
        name = safe_get(obj.Desc, ['Name', 'ObjectName'])
        desc = safe_get(obj.Desc, ['Description'])
    print("{0} | {1} | GUID: {2}".format(name, desc, guid))
"""
    print("Listing all Grasshopper components and their GUIDs...")
    result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
    print("Script result:", result)

if __name__ == "__main__":
    list_grasshopper_components() 