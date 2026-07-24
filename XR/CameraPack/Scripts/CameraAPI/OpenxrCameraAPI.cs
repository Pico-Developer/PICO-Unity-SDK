using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
#if PICO_XR_NEW
using ByteDance.PICO.XR;
#elif PICO_XR_3
using Unity.XR.PXR;
#endif
using UnityEngine;

namespace ByteDance.PICO.CameraPack
{
    public class OpenxrCameraAPI : CameraAPI
    {
        private XrCameraIdPICO targetCamera = XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO;
        XrCameraIdPICO[] availableCameras;
        private CancellationTokenSource cancellationTokenSource; // Cancellation token source

        public override bool GetSupportedState()
        {
            base.GetSupportedState();
            PxrResult ret = PXR_CameraImage.GetAvailableCameras(out availableCameras);
            return ret == PxrResult.SUCCESS;
        }

        public override bool GetCameraImageResolution(PXRCameraEye eye, out Vector2Int[] resolutions)
        {
            var camera = getCameraAvailable(eye);
            PxrResult ret = PXR_CameraImage.GetCameraImageResolutionCapability(camera,
                out PxrExtent2Di[] _resolutions);
            if (ret == PxrResult.SUCCESS)
            {
                resolutions = new Vector2Int[_resolutions.Length];
                for (int i = 0; i < _resolutions.Length; i++)
                {
                    resolutions[i] = new Vector2Int(_resolutions[i].width, _resolutions[i].height);
                }

                return true;
            }

            resolutions = null;
            return false;
        }

        public override bool GetCameraImageFPS(PXRCameraEye eye, out PXRCameraFPS[] fps)
        {
            var camera = getCameraAvailable(eye);
            PxrResult ret = PXR_CameraImage.GetCameraImageFpsCapability(camera, out XrCameraImageFpsPICO[] _fpss);
            if (ret == PxrResult.SUCCESS)
            {
                var fpsList = new System.Collections.Generic.List<PXRCameraFPS>();
                foreach (var f in _fpss)
                {
                    if (f == XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_30_PICO) fpsList.Add(PXRCameraFPS.FPS30);
                    if (f == XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_60_PICO) fpsList.Add(PXRCameraFPS.FPS60);
                }
                fps = fpsList.ToArray();
                return true;
            }
            fps = null;
            return false;
        }

        public override async Task<bool> OpenCameraAsync(PXRCameraEye eye, int width, int height, int fps)
        {
            if (await base.OpenCameraAsync(eye, width, height, fps))
            {
                return true;
            }

            base.OpenCameraAsync(eye, width, height, fps);
            bool cameraAvailable = false;
            cancellationTokenSource = new CancellationTokenSource();
          
            try
            {
                targetCamera = getCameraAvailable(eye);
                if (availableCameras == null)
                {
                    GetSupportedState();
                }

                if (availableCameras == null || availableCameras.Length == 0)
                {
                    Debug.LogError($"OpenxrCameraAPI OpenCameraAsync: No available cameras");
                    return false;
                }
                
                PLog.CameraLog("OpenxrCameraAPI", $"Found {availableCameras.Length} available cameras.");

                foreach (var cameraId in availableCameras)
                {
                    if (cameraId == targetCamera)
                    {
                        cameraAvailable = true;
                        break;
                    }
                }

                if (!cameraAvailable)
                {
                    Debug.LogError($"OpenxrCameraAPI OpenCameraAsync: Camera {eye} unavailable");
                    return false;
                }

                // Check if the camera supports the target resolution
                cameraAvailable = false;
                PxrExtent2Di targetResolution = new PxrExtent2Di(width, height);
                PxrResult ret = PXR_CameraImage.GetCameraImageResolutionCapability(targetCamera,
                    out PxrExtent2Di[] resolutions);
                if (ret == PxrResult.SUCCESS)
                {
                    foreach (var resolution in resolutions)
                    {
                        if (targetResolution.width == resolution.width && targetResolution.height == resolution.height)
                        {
                            cameraAvailable = true;
                            break;
                        }
                    }
                }

                if (!cameraAvailable)
                {
                    Debug.LogError($"OpenxrCameraAPI OpenCameraAsync: Camera {eye} does not support resolution {width}x{height}");
                    return false;
                }

                cameraAvailable = false;
                XrCameraImageFpsPICO targetFps = XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_30_PICO;
                ret = PXR_CameraImage.GetCameraImageFpsCapability(targetCamera,
                    out XrCameraImageFpsPICO[] fpss);
                if (ret == PxrResult.SUCCESS)
                {
                    foreach (var _fps in fpss)
                    {
                        if (_fps == XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_30_PICO && fps == 30)
                        {
                            cameraAvailable = true;
                            targetFps = XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_30_PICO;
                            break;
                        }
                        else if (_fps == XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_60_PICO && fps == 60)
                        {
                            cameraAvailable = true;
                            targetFps = XrCameraImageFpsPICO.XR_CAMERA_IMAGE_FPS_60_PICO;
                            break;
                        }
                    }
                }

                if (!cameraAvailable)
                {
                    Debug.LogError($"OpenxrCameraAPI OpenCameraAsync: Camera {eye} does not support FPS {fps}");
                    return false;
                }

                base.OpenCameraAsync(eye, width, height, fps);
                if (await PXR_CameraImage.CreateCameraDeviceAsync(targetCamera, cancellationTokenSource.Token) !=
                    PxrResult.SUCCESS)
                {
                    Debug.LogError($"OpenxrCameraAPI OpenCameraAsync: Failed to create device for camera {eye}");
                    return false;
                }

                if (await PXR_CameraImage.CreateCameraCaptureSessionAsync(
                        targetCamera,
                        targetResolution.width,
                        targetResolution.height,
                        targetFps,
                        XrCameraImageFormatPICO.XR_CAMERA_IMAGE_FORMAT_RGBA_8888_PICO,
                        XrCameraDataTransferTypePICO.XR_CAMERA_DATA_TRANSFER_TYPE_RAW_BUFFER_PICO,
                        XrCameraModelPICO.XR_CAMERA_MODEL_PINHOLE_PICO, cancellationTokenSource.Token) !=
                    PxrResult.SUCCESS)
                {
                    Debug.LogError($"OpenxrCameraAPI CreateCameraCaptureSessionAsync: Failed to create capture session for camera {eye}");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"OpenxrCameraAPI OpenCameraAsync: {e.Message}");
                return false;
            }

            m_State = PXRCameraStateCode.STATE_CAMERA_OPENED;

            return true;
        }

        public override bool StartPreview()
        {
            if (base.StartPreview())
            {
                return false;
            }
            // 5. Start capture
            if (PXR_CameraImage.BeginCameraCapture(targetCamera) != PxrResult.SUCCESS)
            {
                Debug.LogError($"OpenxrCameraAPI StartPreview: Failed to start capture for camera {m_eye}");
                return false;
            }

            m_State = PXRCameraStateCode.STATE_VIDEO_PREVIEWING;
            return true;
        }

        public override bool StopPreview()
        {
            if (base.StopPreview())
            {
                return true;
            }

          
            // End capture
            PxrResult ret = PXR_CameraImage.EndCameraCapture(targetCamera);
            if (ret != PxrResult.SUCCESS)
            {
                Debug.LogError($"OpenxrCameraAPI StopPreview: Failed to end capture for camera {m_eye}");
                return false;
            }

            m_State = PXRCameraStateCode.STATE_CAMERA_OPENED;
            return true;
        }

        public override bool CloseCamera()
        {
            base.CloseCamera();
            // Destroy capture session
            PxrResult ret = PXR_CameraImage.DestroyCameraCaptureSession(targetCamera);
            if (ret != PxrResult.SUCCESS)
            {
                Debug.LogError($"OpenxrCameraAPI StopPreview: Failed to destroy capture session for camera {m_eye}");
                return false;
            }

            // Destroy device
            ret = PXR_CameraImage.DestroyCameraDevice(targetCamera);
            if (ret != PxrResult.SUCCESS)
            {
                Debug.LogError($"OpenxrCameraAPI StopPreview: Failed to destroy device for camera {m_eye}");
                return false;
            }

            m_State = PXRCameraStateCode.STATE_IDLE;
            return true;
        }

        public override bool AcquireCameraImage(PXRCameraEye eye, out ulong imageId, out Int64 captureTime)
        {
            base.AcquireCameraImage(eye, out imageId, out captureTime);
            return PXR_CameraImage.AcquireCameraImage(getCameraAvailable(eye), 0, out imageId, out captureTime) ==
                   PxrResult.SUCCESS;
        }

        public override bool GetCameraImageData(PXRCameraEye deviceId, ulong imageId,
            out XrCameraImageDataRawBuffer rawBufferData)
        {
            base.GetCameraImageData(deviceId, imageId, out rawBufferData);
            return PXR_CameraImage.GetCameraImageData(getCameraAvailable(deviceId), imageId, out rawBufferData) ==
                   PxrResult.SUCCESS;
        }

        public override bool ReleaseCameraImage(PXRCameraEye deviceId, ulong imageId)
        {
            return PXR_CameraImage.ReleaseCameraImage(getCameraAvailable(deviceId), imageId) == PxrResult.SUCCESS;
        }


        public override bool GetCameraIntrinsics(PXRCameraEye deviceId, out CameraIntrinsics intrinsics)
        {
            intrinsics = new CameraIntrinsics();
            if (m_State == PXRCameraStateCode.STATE_IDLE)
            {
                return base.GetCameraIntrinsics(deviceId, out intrinsics);
            }

            PxrResult ret =
                PXR_CameraImage.GetCameraIntrinsics(getCameraAvailable(deviceId), out XrCameraIntrinsics _intrinsics);
            if (ret == PxrResult.SUCCESS)
            {
                intrinsics.FocalLength = new Vector2(_intrinsics.focalLength.X, _intrinsics.focalLength.Y);
                intrinsics.CornerPoint = new Vector2(_intrinsics.principalPoint.X, _intrinsics.principalPoint.Y);
                intrinsics.Fov = new Vector2(_intrinsics.fov.X, _intrinsics.fov.Y);
                intrinsics.Resolution= new Vector2Int(m_width, m_height);
                return true;
            }

            return false;
        }


        public override bool GetCameraExtrinsics(PXRCameraEye cameraId, out CameraExtrinsics extrinsics)
        {
            extrinsics = new CameraExtrinsics();
            if (m_State == PXRCameraStateCode.STATE_IDLE)
            {
                return base.GetCameraExtrinsics(cameraId, out extrinsics);
            }

            PxrResult ret =
                PXR_CameraImage.GetCameraExtrinsics(getCameraAvailable(cameraId), out XrCameraExtrinsics _extrinsics);
            if (ret == PxrResult.SUCCESS)
            {
                extrinsics.CameraPos = new Vector3(_extrinsics.pose.Position.X, _extrinsics.pose.Position.Y,
                    -_extrinsics.pose.Position.Z);
                extrinsics.CameraRot = new Quaternion(_extrinsics.pose.Orientation.X, _extrinsics.pose.Orientation.Y,
                    -_extrinsics.pose.Orientation.Z, -_extrinsics.pose.Orientation.W);
                return true;
            }

            return false;
        }
 
      
        private XrCameraIdPICO getCameraAvailable(PXRCameraEye eye)
        {
            if (eye == PXRCameraEye.Left)
            {
                return XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO;
            }

            if (eye == PXRCameraEye.Right)
            {
                return XrCameraIdPICO.XR_CAMERA_ID_RGB_RIGHT_PICO;
            }

            return XrCameraIdPICO.XR_CAMERA_ID_RGB_LEFT_PICO;
        }
    }
}