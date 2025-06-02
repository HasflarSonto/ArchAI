using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Newtonsoft.Json.Linq;

namespace RhinoMCP.Functions.Grasshopper.Conversion
{
    public class ConnectionConverter
    {
        private static int _connIdCounter = 0;

        public static JObject ConvertToPython(IGH_Param source, IGH_Param target)
        {
            return new JObject
            {
                ["id"] = $"conn_{_connIdCounter++}",
                ["source"] = new JObject
                {
                    ["component_id"] = source.Attributes.GetTopLevel.DocObject.InstanceGuid.ToString(),
                    ["port"] = source.Name
                },
                ["target"] = new JObject
                {
                    ["component_id"] = target.Attributes.GetTopLevel.DocObject.InstanceGuid.ToString(),
                    ["port"] = target.Name
                },
                ["data_handling"] = GetDataHandling(source, target),
                ["metadata"] = new JObject
                {
                    ["created_at"] = DateTime.UtcNow.ToString("o")
                }
            };
        }

        private static JArray GetDataHandling(IGH_Param source, IGH_Param target)
        {
            var operations = new JArray();

            // Add data handling operations based on parameter types and settings
            if (source.VolatileData is IGH_Structure sourceStructure && target.VolatileData is IGH_Structure targetStructure)
            {
                if (sourceStructure.PathCount > 0 && targetStructure.PathCount > 0)
                {
                    // Both are trees, check if we need to graft or flatten
                    if (sourceStructure.PathCount > targetStructure.PathCount)
                    {
                        operations.Add("flatten");
                    }
                    else if (sourceStructure.PathCount < targetStructure.PathCount)
                    {
                        operations.Add("graft");
                    }
                }
                else if (sourceStructure.PathCount > 0)
                {
                    // Source is tree, target is not - flatten
                    operations.Add("flatten");
                }
                else if (targetStructure.PathCount > 0)
                {
                    // Source is not tree, target is - graft
                    operations.Add("graft");
                }
            }

            // Add simplify if needed
            if (source.VolatileData.DataCount > 1 && target.VolatileData.DataCount == 1)
            {
                operations.Add("simplify");
            }

            return operations;
        }
    }
} 