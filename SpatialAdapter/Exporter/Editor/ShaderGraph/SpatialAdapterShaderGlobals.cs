namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor.ShaderGraph
{
    public static class SpatialAdapterShaderGlobals
    {
        public const string Time = "_Time";
        public const string SinTime = "_SinTime";
        public const string CosTime = "_CosTime";
        public const string DeltaTime = "unity_DeltaTime";
        public const string WorldSpaceCameraPos = "_WorldSpaceCameraPos";
        public const string WorldSpaceCameraDir = "_WorldSpaceCameraDir";
        public const string OrthoParams = "unity_OrthoParams";
        public const string ProjectionParams = "_ProjectionParams";
        public const string ScreenParams = "_ScreenParams";
        public const string ViewMatrix = "UNITY_MATRIX_V";
        public const string ProjectionMatrix = "UNITY_MATRIX_P";
        public const string AmbientSkyColor = "unity_AmbientSky";
        public const string AmbientEquatorColor = "unity_AmbientEquator";
        public const string AmbientGroundColor = "unity_AmbientGround";
    }
}