using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Parameters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RhinoMCP.Functions.Grasshopper.Conversion
{
    public class ComponentConverter
    {
        private static readonly HashSet<string> _valueTypes = new HashSet<string>
        {
            "Point",
            "Plane",
            "Number",
            "Boolean",
            "Vector",
            "Color",
            "Panel"
        };

        private static Dictionary<Guid, string> _semanticIdMap = new Dictionary<Guid, string>();
        private static Dictionary<string, int> _typeCounts = new Dictionary<string, int>();

        public static JObject ConvertToPython(IGH_DocumentObject component)
        {
            string semanticId = GetSemanticId(component);
            var result = new JObject
            {
                ["type"] = component.GetType().Name,
                ["position"] = new JArray(component.Attributes.Pivot.X, component.Attributes.Pivot.Y)
            };

            if (component is IGH_Param param)
            {
                // Embed value for primitives
                if (param.VolatileData.DataCount > 0)
                {
                    var data = param.VolatileData.AllData(true).ToList();
                    result["value"] = ConvertDataToPython(data);
                }
            }
            if (component is IGH_Component ghComponent)
            {
                result["inputs"] = GetComponentInputs(ghComponent);
                result["outputs"] = GetComponentOutputs(ghComponent);
            }

            return new JObject { [semanticId] = result };
        }

        private static string GetSemanticId(IGH_DocumentObject component)
        {
            if (_semanticIdMap.TryGetValue(component.InstanceGuid, out var existing))
                return existing;
            var baseName = component.Name?.Replace(" ", "_").ToLowerInvariant() ?? component.GetType().Name.ToLowerInvariant();
            if (!_typeCounts.ContainsKey(baseName)) _typeCounts[baseName] = 1;
            else _typeCounts[baseName]++;
            var semanticId = _typeCounts[baseName] == 1 ? baseName : $"{baseName}_{_typeCounts[baseName]}";
            _semanticIdMap[component.InstanceGuid] = semanticId;
            return semanticId;
        }

        private static JToken ConvertDataToPython(List<IGH_Goo> data)
        {
            if (data.Count == 1) return ConvertSingleValue(data[0]);
            if (data.All(d => d is IGH_Goo))
                return new JArray(data.Select(ConvertSingleValue));
            return JToken.FromObject(data.Select(ConvertSingleValue));
        }

        private static JToken ConvertSingleValue(IGH_Goo value)
        {
            if (value == null) return null;
            if (value is GH_Integer i) return i.Value;
            if (value is GH_Number n) return n.Value;
            if (value is GH_String s) return s.Value;
            if (value is GH_Boolean b) return b.Value;
            if (value is GH_Point p) return new JArray(p.Value.X, p.Value.Y, p.Value.Z);
            if (value is GH_Colour c) return c.Value.ToArgb();
            // Add more as needed
            return value.ToString();
        }

        private static JObject GetComponentInputs(IGH_Component component)
        {
            var inputs = new JObject();
            foreach (var param in component.Params.Input)
            {
                var input = new JObject
                {
                    ["data_handling"] = GetDataHandling(param),
                    ["connections"] = new JArray(param.Sources.Select(s => $"{GetSemanticId(s.Attributes.GetTopLevel.DocObject)}:{s.Name}"))
                };
                if (param.Sources.Count == 0 && param.VolatileData.DataCount > 0)
                {
                    var data = param.VolatileData.AllData(true).ToList();
                    input["value"] = ConvertDataToPython(data);
                }
                inputs[param.Name] = input;
            }
            return inputs;
        }

        private static JObject GetComponentOutputs(IGH_Component component)
        {
            var outputs = new JObject();
            foreach (var param in component.Params.Output)
            {
                var output = new JObject
                {
                    ["data_handling"] = GetDataHandling(param),
                    ["connections"] = new JArray(param.Recipients.Select(r => $"{GetSemanticId(r.Attributes.GetTopLevel.DocObject)}:{param.Name}"))
                };
                outputs[param.Name] = output;
            }
            return outputs;
        }

        private static JArray GetDataHandling(IGH_Param param)
        {
            var handling = new JArray();
            if (param.Access == GH_ParamAccess.tree) handling.Add("graft");
            if (param.Access == GH_ParamAccess.list) handling.Add("flatten");
            // Add more as needed (reverse, etc.)
            return handling;
        }

        public static JObject GetIdMap() => new JObject(_semanticIdMap.ToDictionary(kv => kv.Value, kv => kv.Key.ToString()));
        public static void ResetSemanticMaps() { _semanticIdMap.Clear(); _typeCounts.Clear(); }

        private static bool ShouldConvertValue(IGH_DocumentObject component)
        {
            var type = component.GetType();
            var typeName = type.Name;

            // Check if it's one of our known value types
            if (_valueTypes.Contains(typeName))
            {
                return true;
            }

            // Check if it's a plugin component
            if (type.Assembly != typeof(GH_Component).Assembly)
            {
                // For plugin components, we'll be conservative and not convert values
                return false;
            }

            return false;
        }

        private static JObject GetComponentType(IGH_DocumentObject component)
        {
            var type = component.GetType();
            var fullName = type.FullName;
            var parts = fullName.Split('.');
            var assemblyName = type.Assembly.GetName().Name;

            return new JObject
            {
                ["category"] = parts.Length > 2 ? parts[2] : "Unknown",
                ["subcategory"] = parts.Length > 3 ? parts[3] : "Unknown",
                ["plugin"] = assemblyName,
                ["full_name"] = fullName,
                ["is_plugin"] = type.Assembly != typeof(GH_Component).Assembly
            };
        }

        private static JObject GetComponentProperties(IGH_Component component)
        {
            var properties = new JObject();

            // Add common properties
            properties["name"] = component.Name;
            properties["description"] = component.Description;
            properties["exposure"] = component.Exposure.ToString();
            properties["is_plugin"] = component.GetType().Assembly != typeof(GH_Component).Assembly;

            // Add component-specific properties
            if (component is IGH_Param param)
            {
                if (param is Param_Number numberParam)
                {
                    // properties["min"] = numberParam.Minimum;
                    // properties["max"] = numberParam.Maximum;
                }
            }

            return properties;
        }
    }
} 