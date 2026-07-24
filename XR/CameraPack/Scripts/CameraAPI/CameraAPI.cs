using System;
using System.Collections;
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
    public delegate void CameraCallBack(int type);

    [Serializable]
    public class CameraAPI : MonoBehaviour
    {
        protected PXRCameraEye m_eye = PXRCameraEye.Left;
        protected int m_height = 0;
        protected int m_width = 0;
        protected int m_fps = 0;
        protected bool isOpened = false;
        protected static PXRCameraStateCode m_State = PXRCameraStateCode.STATE_IDLE;
        protected CancellationTokenSource cancellationTokenSource; // Cancellation token source
        public PXRCameraStateCode GetState() => m_State;
        Int64 captureTime = 0;
        XrCameraImageDataRawBuffer imageData;
        public Action<int, int> OnFirstFrameReceived;
        protected bool _isFirstFrame = true;


        public virtual void OnCameraCreate()
        {
            PLog.CameraLog("CameraAPI", $"OnCameraCreate: Starting CallPluginAtEndOfFrames coroutine.");
            StopCoroutine("CallPluginAtEndOfFrames"); // Ensure only one runs
            StartCoroutine("CallPluginAtEndOfFrames");
        }

        public virtual bool GetSupportedState()
        {
            return false;
        }

        public virtual bool GetCameraImageResolution(PXRCameraEye eye, out Vector2Int[] resolutions)
        {
            resolutions = null;
            return false;
        }

        public virtual bool GetCameraImageFPS(PXRCameraEye eye, out PXRCameraFPS[] fps)
        {
            fps = null;
            return false;
        }

        protected Texture2D _targetTexture;

        public void SetTextureCameraFromUnity(Texture2D targetTexture, int width, int height)
        {
            // Parameter validation (texture must be valid)
            if (targetTexture == null)
            {
                Debug.LogError($"CameraAPI SetTextureCameraFromUnity: targetTexture = null");
                return;
            }

            _targetTexture = targetTexture;
            PLog.CameraLog("CameraAPI", $"SetTextureCameraFromUnity info: {width}x{height}, format: {targetTexture.format}");
        }

        public virtual async Task<bool> OpenCameraAsync(PXRCameraEye eye, int width, int height, int fps)
        {
            m_eye = eye;
            m_width = width;
            m_height = height;
            m_fps = fps;
            _isFirstFrame = true;
            PLog.CameraLog("CameraAPI", $"OpenCameraAsync: Camera {eye} requesting open, resolution {width}x{height}, fps {fps}");
            if (m_State == PXRCameraStateCode.STATE_CAMERA_OPENED ||
                m_State == PXRCameraStateCode.STATE_VIDEO_PREVIEWING)
            {
                Debug.LogError($"CameraAPI OpenCameraAsync: Camera {eye} is already running");
                return true;
            }
            return false;
        }
        
        public virtual bool StartPreview()
        {
            if (m_State == PXRCameraStateCode.STATE_VIDEO_PREVIEWING)
            {
                Debug.LogError($"CameraAPI StartPreview: Camera {m_eye} preview is already running");
                return true;
            }

            if (m_State == PXRCameraStateCode.STATE_IDLE)
            {
                Debug.LogError($"CameraAPI StartPreview: Camera {m_eye} is not open");
                return true;
            }

            return false;
        }
       
        public virtual bool StopPreview()
        {
            if (m_State != PXRCameraStateCode.STATE_VIDEO_PREVIEWING)
            {
                Debug.LogError($"CameraAPI StopPreview: Camera {m_eye} preview is not running");
                return true;
            }


            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }

            return false;
        }

        public virtual bool CloseCamera()
        {
            if (m_State == PXRCameraStateCode.STATE_VIDEO_PREVIEWING)
            {
                StopPreview();
            }

            return false;
        }


        public virtual double GetLatestTimestamp()
        {
            return captureTime;
        }

        public virtual void GetPixels32(Color32[] color32)
        {
            int bytesPerPixel = (int)imageData.bytesPerPixel;
            int stride = (int)imageData.stride;
            int bufferSize = (int)imageData.bufferSize;

            // Check if the buffer is valid
            if (imageData.buffer == IntPtr.Zero)
            {
                Debug.LogError("Buffer pointer is null");
                return;
            }

            byte[] imageData_ = new byte[bufferSize];
            Marshal.Copy(imageData.buffer, imageData_, 0, bufferSize);
            if (color32.Length != m_width * m_height)
            {
                color32 = new Color32[m_width * m_height];
            }

            for (int y = 0; y < m_height; y++)
            {
                for (int x = 0; x < m_width; x++)
                {
                    int index = (y * m_width + x) * 4;
                    color32[y * m_width + x] = new Color32(imageData_[index], imageData_[index + 1],
                        imageData_[index + 2], imageData_[index + 3]);
                }
            }
        }

        public virtual Color GetPixel(int x, int y)
        {
            int bufferSize = (int)imageData.bufferSize;
            int stride = (int)imageData.stride;
            // Check if the buffer is valid
            if (imageData.buffer == IntPtr.Zero)
            {
                Debug.LogError("Buffer pointer is null");
                return Color.clear;
            }

            byte[] imageData_ = new byte[bufferSize];
            Marshal.Copy(imageData.buffer, imageData_, 0, bufferSize);
            return GetColorFromRGBAByteArray(imageData_, m_width, m_height, x, y);
        }

        public PxrSensorState2 GetPredictedMainSensorState2(double predictTime)
        {
            PxrSensorState2 sensorState2 = new PxrSensorState2();
            int sensorFrameIndex = 0;
            PXR_Plugin.Pxr_GetPredictedMainSensorState2(predictTime, ref sensorState2, ref sensorFrameIndex);
            return sensorState2;
        }

        private IEnumerator CallPluginAtEndOfFrames()
        {
            PLog.CameraLog("CameraAPI", "CallPluginAtEndOfFrames started.");
            ulong imageId = 0;
            while (true)
            {
                yield return new WaitForEndOfFrame();
                {
                    bool acquireResult = AcquireCameraImage(m_eye, out imageId, out captureTime);

                    if (acquireResult && imageId > 0)
                    {
                        PLog.CameraLog("CameraAPI", $"CameraAPI: Acquired image {imageId}, captureTime {captureTime}"); // Verbose logging
                        if (GetCameraImageData(m_eye, imageId, out imageData))
                        {
                            if (_isFirstFrame)
                            {
                                PLog.CameraLog("CameraAPI", 
                                    $"CameraAPI: First frame received rsl: {imageData.width}*{imageData.height},before rsl {m_width}*{m_height}");
                                _isFirstFrame = false;
                                m_width = (int)imageData.width;
                                m_height = (int)imageData.height;
                                OnFirstFrameReceived?.Invoke(m_width, m_height);
                            }

                            if (_targetTexture != null)
                            {
                                _targetTexture.LoadRawTextureData(imageData.buffer, (int)imageData.bufferSize);
                                _targetTexture.Apply();
                            }
                            ReleaseCameraImage(m_eye, imageId);
                        }
                        else
                        {
                            PLog.CameraLog("CameraAPI", $"CameraAPI: Failed to get image data for imageId {imageId}");
                        }
                    }
                    else if (acquireResult)
                    {
                        PLog.CameraLog("CameraAPI", "CameraAPI: Acquired image but ID is 0");
                    } // Optional debug
                }
            }
        }

        public virtual bool AcquireCameraImage(PXRCameraEye eye, out ulong imageId, out Int64 captureTime)
        {
            imageId = 0;
            captureTime = 0;
            return false;
        }

        public virtual bool GetCameraImageData(PXRCameraEye deviceId, ulong imageId,
            out XrCameraImageDataRawBuffer rawBufferData)
        {
            rawBufferData = new XrCameraImageDataRawBuffer();
            return false;
        }

        public virtual bool ReleaseCameraImage(PXRCameraEye deviceId, ulong imageId)
        {
            return false;
        }

        public virtual bool GetCameraIntrinsics(PXRCameraEye cameraId, out CameraIntrinsics intrinsics)
        {
            intrinsics = new CameraIntrinsics();
            return false;
        }


        public virtual bool GetCameraExtrinsics(PXRCameraEye cameraId, out CameraExtrinsics extrinsics)
        {
            extrinsics = new CameraExtrinsics();
            return false;
        }

        public Color GetColorFromRGBAByteArray(byte[] byteArray, int width, int height, int x, int y)
        {
            // Validate parameters
            if (byteArray == null || byteArray.Length == 0)
            {
                Debug.LogError("Byte array is null or empty");
                return Color.clear;
            }

            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                Debug.LogError($"Coordinates ({x},{y}) out of image bounds (Width: {width}, Height: {height})");
                return Color.clear;
            }

            int requiredLength = width * height * 4;
            if (byteArray.Length != requiredLength)
            {
                Debug.LogError($"Byte array length mismatch: actual {byteArray.Length}, expected {requiredLength} (RGBA8888 format)");
                return Color.clear;
            }

            int pixelIndex = (y * width + x) * 4;

            byte r = byteArray[pixelIndex];
            byte g = byteArray[pixelIndex + 1];
            byte b = byteArray[pixelIndex + 2];
            byte a = byteArray[pixelIndex + 3];

            PLog.CameraLog("CameraAPI", $"GetColorFromRGBAByteArray Pixel ({x},{y}) Color: R={r}, G={g}, B={b}, A={a}");
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }

        public virtual void OnApplicationPause(bool pauseStatus)
        {
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }

    public enum PXRCameraEye
    {
        Left,
        Right
    }

    public enum PXRCameraFPS
    {
        FPS15 = 15,
        FPS30 = 30,
        FPS45 = 45,
        FPS60 = 60
    }

    public enum PXRCameraStateCode
    {
        STATE_IDLE = 0, // Initial state
        STATE_CAMERA_OPENED, // Camera opened
        STATE_VIDEO_PREVIEWING, // Entering preview state
    }

    public struct CameraIntrinsics
    {
        /// <summary>
        /// Focal length in pixels (horizontal/vertical)
        /// </summary>
        public Vector2 FocalLength;

        /// <summary>
        /// Principal point coordinates (optical center on image plane, unit: pixels)
        /// </summary>
        public Vector2 CornerPoint;

        /// <summary>
        /// Image resolution (width/height)
        /// </summary>
        public Vector2 Resolution;

        /// <summary>
        /// Field of view angles (horizontal/vertical)
        /// </summary>
        public Vector2 Fov;
    }

    public struct CameraExtrinsics
    {
        public Vector3 CameraPos;
        public Quaternion CameraRot;
    }
}