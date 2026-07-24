using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ByteDance.PICO.SpatialAdapter.Exporter.Editor
{
    // TODO: Remove once a better solution for empty strings/meshes has been found. 
    //      
    //       We are disabling TMP_Input because they override TMP text and we need to set some
    //       temporary text into all text meshes to avoid empty vertices which causes an
    //       issue on the SpatialEngine side. 
    //
    //       Meegos:
    //          https://meego.larkoffice.com/pico/issue/detail/7080761504?parentUrl=%2Fworkbench&openScene=3
    //          https://meego.larkoffice.com/pico/issue/detail/7246101506?parentUrl=%2Fworkbench&openScene=3
    internal static class TemporaryTextMeshProExportScope
    {
        private static List<TMP_InputField> _textInputs = new List<TMP_InputField>();
        private static bool[] _textInputEnabledStates;
        private static List<Tuple<TMP_Text, string>> _textMeshRestorePairs = new List<Tuple<TMP_Text, string>>();
        private static bool initialized = false;
        
        public static void SetTextMeshProsTemporaryData(GameObject[] roots)
        {
            _textInputs.Clear();
            _textMeshRestorePairs.Clear();

            var _textMeshes = new List<TMP_Text>();
            foreach (var root in roots)
            {
                _textInputs.AddRange(root.GetComponentsInChildren<TMP_InputField>());
                _textMeshes.AddRange(root.GetComponentsInChildren<TMP_Text>());
            }

            _textInputEnabledStates = new bool[_textInputs.Count];

            for (int i = 0; i < _textInputs.Count; i++)
            {
                var textInput = _textInputs[i];
                _textInputEnabledStates[i] = textInput.enabled;
                textInput.enabled = false;
            }

            for (int i = 0; i < _textMeshes.Count; i++)
            {
                if ((_textMeshes[i].text.Trim().Length > 0))
                {
                    // Check for zero width space value that TMP_Input inserts into its text meshes 
                    if ((int)_textMeshes[i].text[0] != 8203)
                        continue;
                }
                
                var textMesh = _textMeshes[i];
                // Still store text because user could insert deliberate white space 
                _textMeshRestorePairs.Add(new Tuple<TMP_Text, string>(textMesh, textMesh.text));
                textMesh.text = "_";
                textMesh.ForceMeshUpdate(true, true);
            }

            initialized = true;
        }
        

        public static void RestoreTextMeshProsFromTemporaryData()
        {
            if (!initialized)
            {
                return;
            }

            for (int i = 0; i < _textMeshRestorePairs.Count; i++)
            {
                var textMesh = _textMeshRestorePairs[i].Item1;
                if (!textMesh)
                {
                    continue;
                }

                textMesh.text = _textMeshRestorePairs[i].Item2;
                textMesh.ForceMeshUpdate(true, true);
            }

            for (int i = 0; i < _textInputs.Count; i++)
            {
                var textInput = _textInputs[i];
                if (!textInput)
                {
                    continue;
                }

                textInput.enabled = _textInputEnabledStates[i];
            }
        }
    }
}
