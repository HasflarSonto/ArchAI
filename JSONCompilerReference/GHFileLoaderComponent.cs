using System;
using Grasshopper.Kernel;
using GHUI.Classes;

namespace GHUI
{
    public class GHFileLoaderComponent : GH_Component
    {
        public GHFileLoaderComponent()
            : base("Load GH File", "LoadGH",
                "Loads a Grasshopper (.gh) file into the current document using clipboard operations",
                "UI", "File")
        {
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("98765432-5432-5432-5432-987654321098");

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("File Path", "P", "Path to the Grasshopper (.gh) file", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "S", "Status of the operation", GH_ParamAccess.item);
            pManager.AddTextParameter("Debug", "D", "Debug information", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filePath = null;
            if (!DA.GetData(0, ref filePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No file path provided");
                return;
            }

            // Validate file path
            if (string.IsNullOrEmpty(filePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "File path is null or empty");
                DA.SetData(0, "Error: File path is null or empty");
                DA.SetData(1, "File path is null or empty");
                return;
            }

            // Check if file exists
            if (!System.IO.File.Exists(filePath))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"File not found at: {filePath}");
                DA.SetData(0, $"Error: File not found at {filePath}");
                DA.SetData(1, $"File not found at {filePath}");
                return;
            }

            // Get document with validation
            var doc = OnPingDocument();
            if (doc == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to get active document");
                DA.SetData(0, "Error: Failed to get active document");
                DA.SetData(1, "Failed to get active document");
                return;
            }

            try
            {
                // Load the file using the new approach
                GHFileLoader.LoadGHFile(doc, filePath);
                
                // Get the debug output
                string debugOutput = GHFileLoader.GetDebugOutput();
                
                // Check if the operation was successful
                if (debugOutput.Contains("Error:"))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to load file");
                    DA.SetData(0, "Error");
                }
                else
                {
                    DA.SetData(0, "Success");
                }
                
                DA.SetData(1, debugOutput);
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error: {ex.Message}\nStack trace: {ex.StackTrace}";
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                DA.SetData(0, "Error");
                DA.SetData(1, errorMessage);
            }
        }
    }
} 