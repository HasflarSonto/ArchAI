using System;
using System.Collections.Generic;
using System.Drawing;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using rhinomcp.Serializers;
using Rhino.PlugIns;
using Rhino.Commands;

namespace RhinoMCPPlugin.Functions;

public partial class RhinoMCPFunctions
{
    public JObject OpenGrasshopper(JObject parameters)
    {
        try
        {
            // Execute the Grasshopper command
            bool success = RhinoApp.RunScript("Grasshopper", false);
            
            if (success)
            {
                return new JObject
                {
                    ["status"] = "success",
                    ["message"] = "Grasshopper opened successfully"
                };
            }
            else
            {
                return new JObject
                {
                    ["status"] = "error",
                    ["message"] = "Failed to open Grasshopper"
                };
            }
        }
        catch (Exception e)
        {
            return new JObject
            {
                ["status"] = "error",
                ["message"] = e.Message
            };
        }
    }
} 