using System;
using System.Drawing;
using System.Linq;
using Newtonsoft.Json.Linq;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using rhinomcp.Serializers;

namespace RhinoMCPPlugin.Functions;

public partial class RhinoMCPFunctions
{
    public JObject ModifyObjects(JObject parameters)
    {
        bool all = parameters.ContainsKey("all");
        JArray objectParameters = (JArray)parameters["objects"];
        
        var doc = RhinoDoc.ActiveDoc;
        var objects = doc.Objects.ToList();
        
        if (all && objectParameters.Count == 1)
        {
            // Get the first modification parameters (excluding the "all" property)
            JObject firstModification = (JObject)objectParameters.FirstOrDefault()!;
            
            // Create new parameters object with all object IDs
            foreach (var obj in objects)
            {
                // Create a new copy of the modification parameters for each object
                JObject newModification = new JObject(firstModification) { ["id"] = obj.Id.ToString() };
                objectParameters.Add(newModification);
            }
        }

        var i = 0;
        foreach (JObject parameter in objectParameters)
        {
            if (parameter.ContainsKey("id"))
            {
                ModifyObject(parameter);
                i++;
            }
        }
        doc.Views.Redraw();
        return new JObject() { ["modified"] = i };
    }
}