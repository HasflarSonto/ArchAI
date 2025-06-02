using System;
using Grasshopper.Kernel;
using GHUI.Classes;

namespace GHUI
{
    public class JSONComponentLoaderComponent : GH_Component
    {
        public JSONComponentLoaderComponent()
            : base("Load JSON Components", "LoadJSON",
                "Loads Grasshopper components from a JSON file",
                "UI", "Support")
        {
        }

        public override Guid ComponentGuid => new Guid("12345678-1234-1234-1234-123456789013");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("JSON Path", "path", "Path to the JSON file containing component definitions", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "status", "Status of the operation", GH_ParamAccess.item);
            pManager.AddTextParameter("Debug", "debug", "Debug information", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string jsonPath = string.Empty;
            if (!DA.GetData(0, ref jsonPath)) return;

            try
            {
                var doc = OnPingDocument();
                if (doc == null)
                {
                    DA.SetData(0, "Error: No active document");
                    DA.SetData(1, "Failed to get document reference");
                    return;
                }

                JSONComponentLoader.LoadComponentsFromJSON(doc, jsonPath);
                string debugOutput = JSONComponentLoader.GetDebugOutput();
                
                DA.SetData(0, "Components loaded successfully");
                DA.SetData(1, debugOutput);
            }
            catch (Exception ex)
            {
                string debugOutput = JSONComponentLoader.GetDebugOutput();
                debugOutput += $"Error: {ex.Message}\n{ex.StackTrace}\n";
                DA.SetData(0, $"Error loading components: {ex.Message}");
                DA.SetData(1, debugOutput);
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override System.Drawing.Bitmap Icon => null;
    }
} 