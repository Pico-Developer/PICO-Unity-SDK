using UnityEngine;
using System.Collections.Generic;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    /// <summary>
    /// Interface for asset exporters in the SpatialAdapter system
    /// </summary>
    internal class SpatialAdapterAssetExporter<T>
    {
        public List<PathLookUp<T>> PathLookUps
        {
            get;
            private set;
        }

        protected void AddExportedPath(T assetId, string exportedPath)
        {
            PathLookUps.Add(new PathLookUp<T>(assetId, exportedPath.Relativize()));
        }

        protected SpatialAdapterExporterSettings settings;

        private SpatialAdapterAssetExporter() {}

        public SpatialAdapterAssetExporter(SpatialAdapterExporterSettings inSettings)
        {
            settings = inSettings;
            PathLookUps = new();
        }

        // Returns whether or not this asset shouuld be registered for this current export process.
        // If yes, the AssetExportManager will avoid re-exporting this.
        // If no, the AssetExportManager will re-export this asset if encountered again.
        public virtual bool RegisterAsset(string assetPath, string exportedPath, bool isAssetDirty)
        {
            return true;
        }
    }
}