using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;

namespace RhinoMCP.Functions.Grasshopper.Conversion
{
    public class GrasshopperToPythonConverter
    {
        private readonly Dictionary<Guid, string> _idMapping = new Dictionary<Guid, string>();

        public JObject ConvertDocument(GH_Document document)
        {
            ComponentConverter.ResetSemanticMaps();
            var components = new JObject();
            foreach (var obj in document.Objects)
            {
                if (obj is IGH_Component || obj is IGH_Param)
                {
                    var compObj = ComponentConverter.ConvertToPython(obj);
                    foreach (var prop in compObj)
                        components[prop.Key] = prop.Value;
                }
            }
            var result = new JObject
            {
                ["components"] = components,
                ["id_map"] = ComponentConverter.GetIdMap()
            };
            return result;
        }

        public JObject ConvertComponent(IGH_DocumentObject component)
        {
            var result = ComponentConverter.ConvertToPython(component);
            _idMapping[component.InstanceGuid] = result["id"].ToString();
            return result;
        }

        public JObject ConvertConnection(IGH_Param source, IGH_Param target)
        {
            return ConnectionConverter.ConvertToPython(source, target);
        }

        public string GetPythonId(Guid grasshopperId)
        {
            return _idMapping.TryGetValue(grasshopperId, out var pythonId) ? pythonId : null;
        }

        public void ClearIdMapping()
        {
            _idMapping.Clear();
        }
    }
} 