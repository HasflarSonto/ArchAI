using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace RhinoMCP.Functions.Grasshopper.Conversion
{
    public class PythonToGrasshopperConverter
    {
        private readonly Dictionary<string, Guid> _idMapping = new Dictionary<string, Guid>();
        private readonly GH_Document _document;
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

        public PythonToGrasshopperConverter(GH_Document document)
        {
            _document = document;
        }

        public void ConvertDocument(JObject pythonDocument)
        {
            var idMap = pythonDocument["id_map"] as JObject;
            var components = pythonDocument["components"] as JObject;
            if (components == null) return;
            // First pass: create all components
            foreach (var compProp in components)
            {
                var semanticId = compProp.Key;
                var compObj = compProp.Value as JObject;
                var ghObj = ConvertComponent(semanticId, compObj);
                if (ghObj != null && idMap != null && idMap[semanticId] != null)
                    _idMapping[semanticId] = Guid.Parse(idMap[semanticId].ToString());
            }
            // Second pass: create all connections
            foreach (var compProp in components)
            {
                var semanticId = compProp.Key;
                var compObj = compProp.Value as JObject;
                if (compObj == null) continue;
                if (compObj["outputs"] is JObject outputs)
                {
                    foreach (var outputProp in outputs)
                    {
                        var portName = outputProp.Key;
                        var outputObj = outputProp.Value as JObject;
                        if (outputObj == null || outputObj["connections"] == null) continue;
                        foreach (var conn in outputObj["connections"] as JArray)
                        {
                            var connStr = conn.ToString();
                            var parts = connStr.Split(':');
                            if (parts.Length != 2) continue;
                            var targetId = parts[0];
                            var targetPort = parts[1];
                            CreateConnection(
                                GetComponentBySemanticId(semanticId), portName,
                                GetComponentBySemanticId(targetId), targetPort
                            );
                        }
                    }
                }
            }
        }

        private IGH_DocumentObject GetComponentBySemanticId(string semanticId)
        {
            if (_idMapping.TryGetValue(semanticId, out var guid))
                return _document.Objects.FirstOrDefault(o => o.InstanceGuid == guid);
            return null;
        }

        public IGH_DocumentObject ConvertComponent(string semanticId, JObject pythonComponent)
        {
            if (pythonComponent == null) return null;

            // Get component type information
            var typeInfo = pythonComponent["type"] as JObject;
            var fullName = typeInfo["full_name"].ToString();
            var isPlugin = (bool)typeInfo["is_plugin"];

            // Create the component
            var component = CreateComponent(fullName, isPlugin);
            if (component == null) return null;

            // Set position
            var position = pythonComponent["position"] as JArray;
            if (position != null)
            {
                component.Attributes.Pivot = new System.Drawing.PointF(
                    (float)position[0],
                    (float)position[1]
                );
            }

            // Set properties
            var properties = pythonComponent["properties"] as JObject;
            if (properties != null)
            {
                SetComponentProperties(component, properties);
            }

            // Set values only for specific component types
            if (ShouldConvertValue(component))
            {
                var value = pythonComponent["value"] as JObject;
                if (value != null)
                {
                    SetComponentValue(component, value);
                }
            }

            // Store ID mapping
            _idMapping[semanticId] = component.InstanceGuid;

            return component;
        }

        private bool ShouldConvertValue(IGH_DocumentObject component)
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

        private IGH_DocumentObject CreateComponent(string fullName, bool isPlugin)
        {
            // Get the component type
            var type = Type.GetType(fullName);
            if (type == null)
            {
                // If it's a plugin component and we can't find the type,
                // we'll create a placeholder component
                if (isPlugin)
                {
                    return CreatePlaceholderComponent(fullName);
                }
                return null;
            }

            // Create the component
            var component = Activator.CreateInstance(type) as IGH_DocumentObject;
            if (component == null) return null;

            // Add to document
            _document.AddObject(component, false);
            return component;
        }

        private IGH_DocumentObject CreatePlaceholderComponent(string fullName)
        {
            // Create a panel component as a placeholder
            var panel = new GH_Panel();
            panel.Name = $"Placeholder: {fullName}";
            panel.Description = "This is a placeholder for a plugin component that could not be loaded.";
            _document.AddObject(panel, false);
            return panel;
        }

        private void SetComponentProperties(IGH_DocumentObject component, JObject properties)
        {
            if (component is IGH_Component ghComponent)
            {
                // Set name if available
                if (properties["name"] != null)
                {
                    ghComponent.Name = properties["name"].ToString();
                }

                // Set description if available
                if (properties["description"] != null)
                {
                    ghComponent.Description = properties["description"].ToString();
                }

                // Set component-specific properties
                if (ghComponent is IGH_Param param)
                {
                    if (param is Param_Number numberParam)
                    {
                        if (properties["value"] != null)
                        {
                            var persistentParam = numberParam as GH_PersistentParam<GH_Number>;
                            if (persistentParam != null)
                            {
                                persistentParam.PersistentData.Clear();
                                persistentParam.PersistentData.Append(new GH_Number((double)properties["value"]));
                            }
                        }
                    }
                }
            }
        }

        private void SetComponentValue(IGH_DocumentObject component, JObject value)
        {
            if (component is IGH_Component ghComponent)
            {
                var data = value["data"] as JArray;
                var structure = value["structure"] as JObject;

                if (data != null && structure != null)
                {
                    var ghData = DataStructureConverter.ConvertToGrasshopper(value);
                    
                    // Set the data for each output parameter
                    foreach (var param in ghComponent.Params.Output)
                    {
                        if (ShouldConvertValue(param))
                        {
                            param.VolatileData.Clear();
                            // Assign data branch by branch
                            for (int i = 0; i < ghData.PathCount; i++)
                            {
                                var path = ghData.Paths[i];
                                var branch = ghData.get_Branch(path);
                                foreach (var item in branch)
                                {
                                    if (item is IGH_Goo goo && path is GH_Path ghPath)
                                    {
                                        var persistentParam = param as GH_PersistentParam<IGH_Goo>;
                                        if (persistentParam != null)
                                        {
                                            persistentParam.PersistentData.Append(goo, ghPath);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CreateConnection(IGH_DocumentObject source, string sourcePort, IGH_DocumentObject target, string targetPort)
        {
            if (source is IGH_Component sourceComponent && target is IGH_Component targetComponent)
            {
                var sourceParam = sourceComponent.Params.Output.FirstOrDefault(p => p.Name == sourcePort);
                var targetParam = targetComponent.Params.Input.FirstOrDefault(p => p.Name == targetPort);

                if (sourceParam != null && targetParam != null)
                {
                    // Create the connection
                    // _document.AddConnection(new Grasshopper.Kernel.GH_Connection(sourceParam, targetParam));
                }
            }
        }

        public Guid GetGrasshopperId(string pythonId)
        {
            return _idMapping.TryGetValue(pythonId, out var grasshopperId) ? grasshopperId : Guid.Empty;
        }

        public void ClearIdMapping()
        {
            _idMapping.Clear();
        }
    }
} 