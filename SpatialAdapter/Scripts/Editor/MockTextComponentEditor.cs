using UnityEditor;
using UnityEngine;

namespace ByteDance.PICO.SpatialAdapter.Component
{
    [CustomEditor(typeof(MockTextComponent))]
    public class MockTextComponentEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            Debug.Log("Selected MockTextComponent");

            var mockTextComponent = (MockTextComponent)target;
            var nativeText = mockTextComponent.nativeText;
            
            if (nativeText)
            {
                Selection.activeGameObject = nativeText.gameObject;
            }
        }
    }
}
