from rhinomcp import get_rhino_connection

def open_grasshopper():
    """
    Open Grasshopper in Rhino using the MCP connection.
    """
    rhino = get_rhino_connection()
    
    print("Opening Grasshopper...")
    result = rhino.send_command("open_grasshopper", {})
    print("Result:", result)

if __name__ == "__main__":
    open_grasshopper() 