#if AR_FOUNDATION_6_IMAGE_TRACKING
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    internal static class ImageTrackingPoseConverter
    {
        // PICO image anchors use local XY as the image plane (+Z normal),
        // while AR Foundation trackables use local XZ (+Y normal).
        private static readonly Quaternion ImagePlaneCorrection =
            Quaternion.Euler(-90f, 0f, 0f);

        // PICO's image-facing convention is opposite to the AR Foundation
        // prefab convention. Flip the in-plane X axis and plane normal while
        // preserving the image's vertical axis.
        private static readonly Quaternion ImageFacingCorrection =
            Quaternion.Euler(0f, 0f, 180f);
        private const ulong ValidFlags =
            XrSpaceLocationFlags.XR_SPACE_LOCATION_ORIENTATION_VALID_BIT
            | XrSpaceLocationFlags.XR_SPACE_LOCATION_POSITION_VALID_BIT;

        private const ulong TrackedFlags =
            XrSpaceLocationFlags.XR_SPACE_LOCATION_ORIENTATION_TRACKED_BIT
            | XrSpaceLocationFlags.XR_SPACE_LOCATION_POSITION_TRACKED_BIT;

        internal static void Convert(
            SpatialImageTrackingLocation location,
            out Pose pose,
            out TrackingState trackingState)
        {
            if ((location.LocationFlags & ValidFlags) != ValidFlags)
            {
                pose = Pose.identity;
                trackingState = TrackingState.None;
                return;
            }

            PxrPosef nativePose = location.Pose;
            pose = new Pose(
                new Vector3(
                    nativePose.position.x,
                    nativePose.position.y,
                    -nativePose.position.z),
                new Quaternion(
                    nativePose.orientation.x,
                    nativePose.orientation.y,
                    -nativePose.orientation.z,
                    -nativePose.orientation.w)
                    * ImagePlaneCorrection
                    * ImageFacingCorrection);
            trackingState = (location.LocationFlags & TrackedFlags) == TrackedFlags
                ? TrackingState.Tracking
                : TrackingState.Limited;
        }
    }
}
#endif
