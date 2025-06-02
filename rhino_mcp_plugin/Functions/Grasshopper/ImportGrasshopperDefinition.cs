using System;
using Newtonsoft.Json.Linq;
using Rhino;
using Grasshopper.Kernel;
using RhinoMCP.Functions.Grasshopper.Conversion;

namespace RhinoMCPPlugin.Functions;

public partial class RhinoMCPFunctions
{
    public JObject ImportGrasshopperDefinition(JObject parameters)
    {
        try
        {
            // Get the JSON definition from parameters
            JObject definition = (JObject)parameters["definition"];
            if (definition == null)
            {
                throw new ArgumentException("Definition parameter is required");
            }

            // Get the active Grasshopper document
            var ghDoc = Grasshopper.Instances.ActiveCanvas?.Document;
            if (ghDoc == null)
            {
                throw new InvalidOperationException("No active Grasshopper document found");
            }

            // Create converter and convert the definition
            var converter = new PythonToGrasshopperConverter(ghDoc);
            converter.ConvertDocument(definition);

            // Update the canvas
            ghDoc.NewSolution(true);

            return new JObject
            {
                ["status"] = "success",
                ["message"] = "Grasshopper definition imported successfully"
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