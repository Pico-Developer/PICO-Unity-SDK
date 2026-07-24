#if UNITY_INCLUDE_XRI
using System;
using System.Collections.Generic;
using ByteDance.PICO.SpatialAdapter;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace XR.Interactors
{
    /// <summary>
    /// Attach this to a GameObject to make an pointer that a user can use to interact with a scene.
    /// An XRInteractionManager must be present in the scene, and a SpatialInputState must be assigned to this object.
    /// Adding XRInteractable-derived components to GameObjects will allow those to be interacted with by an XRInteractor
    ///
    /// For further reference see the Unity XR Interactor Toolkit reference:
    ///     https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.0/manual/get-started.html 
    /// </summary>

#if UNITY_INCLUDE_XRI_3_0
    public class XRSpatialPointerInteractor : UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor
#else
#if UNITY_6000_0_OR_NEWER
    public class XRSpatialPointerInteractor : UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor
#else
    public class XRSpatialPointerInteractor : UnityEngine.XR.Interaction.Toolkit.XRBaseInteractor
#endif

#endif
    {
        [SerializeField]
        InputActionReference m_SpatialPointer;
        SpatialInputState m_SpatialPointerState;

        protected override void Start()
        {
            EnhancedTouchSupport.Enable();  

            base.Start();

            m_SpatialPointer.action.performed += OnWorldTouchPerformed;
            m_SpatialPointer.action.canceled += OnWorldTouchCancelled;
        }

        protected override void OnDestroy()
        {
            m_SpatialPointer.action.performed -= OnWorldTouchPerformed;
            m_SpatialPointer.action.canceled -= OnWorldTouchCancelled;
            base.OnDestroy();
        }

        void OnWorldTouchPerformed(InputAction.CallbackContext context)
        {
            m_SpatialPointerState = context.ReadValue<SpatialInputState>();
            transform.position = m_SpatialPointerState.currentPosition; 
            transform.rotation = m_SpatialPointerState.deviceRotation;
        }

        void OnWorldTouchCancelled(InputAction.CallbackContext context)
        {
            m_SpatialPointerState = context.ReadValue<SpatialInputState>();
        }

        public override bool isSelectActive
        {
            get
            {
                switch (m_SpatialPointerState.phase)
                {
                    case TouchPhase.Began:
                    case TouchPhase.Moved:
                        return base.isSelectActive;
                    case TouchPhase.Ended:
                    case TouchPhase.None:
                    case TouchPhase.Canceled:
                        return false;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
#if UNITY_INCLUDE_XRI_3_0 || UNITY_6000_0_OR_NEWER
    public override bool CanHover(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRHoverInteractable interactable)
#else
        public override bool CanHover(UnityEngine.XR.Interaction.Toolkit.IXRHoverInteractable interactable)
#endif
        {
            return base.CanHover(interactable) && (!hasSelection || IsSelecting(interactable));
        }

#if UNITY_INCLUDE_XRI_3_0 || UNITY_6000_0_OR_NEWER
    public override bool CanSelect(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable)
#else
        public override bool CanSelect(UnityEngine.XR.Interaction.Toolkit.IXRSelectInteractable interactable)
#endif
        {
            return base.CanSelect(interactable) && (!hasSelection || IsSelecting(interactable));
        }
#if UNITY_INCLUDE_XRI_3_0 || UNITY_6000_0_OR_NEWER
        public override void GetValidTargets(List<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable> targets)
#else
        public override void GetValidTargets(List<UnityEngine.XR.Interaction.Toolkit.IXRInteractable> targets)
#endif
        {
            targets.Clear();
            switch (m_SpatialPointerState.phase)
            {
                case TouchPhase.None:
                    break;
                case TouchPhase.Began:
                case TouchPhase.Moved:
                case TouchPhase.Canceled:
                case TouchPhase.Ended:
                    if (TryGetInteractable(m_SpatialPointerState.targetObject, out var interactable))
                        targets.Add(interactable);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

#if UNITY_INCLUDE_XRI_3_0 || UNITY_6000_0_OR_NEWER
        bool TryGetInteractable(GameObject go, out UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable interactable)
#else
        bool TryGetInteractable(GameObject go, out UnityEngine.XR.Interaction.Toolkit.IXRInteractable interactable)
#endif
        {
            if (go == null)
            {
                interactable = null;
                return false;
            }

            return interactionManager.TryGetInteractableForCollider(go.GetComponent<Collider>(), out interactable);
        }
    }
}
#endif
