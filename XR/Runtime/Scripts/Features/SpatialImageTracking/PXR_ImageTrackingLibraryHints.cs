#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    [Serializable]
    public struct PXR_ImageTrackingReferenceHint
    {
        [SerializeField]
        private string m_ImageName;

        [SerializeField]
        private string m_ImageGuid;

        [SerializeField]
        private bool m_OptimizeForStaticImage;

        public PXR_ImageTrackingReferenceHint(
            string imageName,
            Guid imageGuid,
            bool optimizeForStaticImage)
        {
            m_ImageName = imageName;
            m_ImageGuid = imageGuid.ToString("D");
            m_OptimizeForStaticImage = optimizeForStaticImage;
        }

        public string ImageName => m_ImageName;

        public Guid ImageGuid => Guid.TryParse(m_ImageGuid, out Guid guid)
            ? guid
            : Guid.Empty;

        public bool OptimizeForStaticImage => m_OptimizeForStaticImage;
    }

    [CreateAssetMenu(menuName = "PICO/XR/Image Tracking Library Hints")]
    public sealed class PXR_ImageTrackingLibraryHints : ScriptableObject
    {
        [SerializeField]
        private XRReferenceImageLibrary m_ReferenceLibrary;

        [SerializeField]
        private List<PXR_ImageTrackingReferenceHint> m_Images =
            new List<PXR_ImageTrackingReferenceHint>();

        public XRReferenceImageLibrary ReferenceLibrary => m_ReferenceLibrary;

        public IReadOnlyList<PXR_ImageTrackingReferenceHint> Images => m_Images;

        public bool TryGetOptimizeForStaticImage(
            Guid imageGuid,
            string imageName,
            out bool optimizeForStaticImage)
        {
            for (int index = 0; index < m_Images.Count; index++)
            {
                PXR_ImageTrackingReferenceHint hint = m_Images[index];
                if (hint.ImageGuid != Guid.Empty && hint.ImageGuid == imageGuid)
                {
                    optimizeForStaticImage = hint.OptimizeForStaticImage;
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(imageName))
            {
                for (int index = 0; index < m_Images.Count; index++)
                {
                    PXR_ImageTrackingReferenceHint hint = m_Images[index];
                    if (string.Equals(
                            hint.ImageName,
                            imageName,
                            StringComparison.Ordinal))
                    {
                        optimizeForStaticImage = hint.OptimizeForStaticImage;
                        return true;
                    }
                }
            }

            optimizeForStaticImage = false;
            return false;
        }
    }
}
#endif

