using System;
using Newtonsoft.Json.Linq;
using Rhino;
using Grasshopper.Kernel;
using RhinoMCP.Functions.Grasshopper.Conversion;

namespace RhinoMCPPlugin.Functions;

public partial class RhinoMCPFunctions
{
    public JObject ExportGrasshopperDefinition(JObject parameters)
    {
        try
        {
            // Get the active Grasshopper document
            var ghDoc = Grasshopper.Instances.ActiveCanvas?.Document;
            if (ghDoc == null)
            {
                throw new InvalidOperationException("No active Grasshopper document found");
            }

            // Create converter and convert the definition
            var converter = new GrasshopperToPythonConverter();
            JObject definition = converter.ConvertDocument(ghDoc);

            return new JObject
            {
                ["status"] = "success",
                ["definition"] = definition
            };
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