#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using Unity.Collections;
using UnityEngine;

namespace ByteDance.PICO.XR
{
    internal static class ImageReferenceTextureCapture
    {
        internal static byte[] CaptureCanonicalRgba(Texture2D texture)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            if (texture.isReadable && texture.format == TextureFormat.RGBA32)
            {
                NativeArray<byte> source = texture.GetPixelData<byte>(0);
                int expectedLength = checked(texture.width * texture.height * 4);
                if (source.Length < expectedLength)
                {
                    throw new InvalidOperationException(
                        "The readable texture does not contain a complete RGBA base mip.");
                }

                var pixels = new byte[expectedLength];
                NativeArray<byte>.Copy(source, pixels, expectedLength);
                return pixels;
            }

            return CaptureWithGpuReadback(texture);
        }

        private static byte[] CaptureWithGpuReadback(Texture2D texture)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = null;
            Texture2D readback = null;
            try
            {
                bool isDataSrgb = texture.isDataSRGB;
                // The OpenXR database consumes encoded image bytes, like the
                // RGB888 output of an image decoder. Preserve sRGB encoding
                // instead of gamma-decoding it in Linear-color-space projects.
                temporary = RenderTexture.GetTemporary(
                    texture.width,
                    texture.height,
                    0,
                    RenderTextureFormat.ARGB32,
                    isDataSrgb
                        ? RenderTextureReadWrite.sRGB
                        : RenderTextureReadWrite.Linear);
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                readback = new Texture2D(
                    texture.width,
                    texture.height,
                    TextureFormat.RGBA32,
                    false,
                    !isDataSrgb);
                readback.ReadPixels(
                    new Rect(0, 0, texture.width, texture.height),
                    0,
                    0,
                    false);
                readback.Apply(false, false);

                NativeArray<byte> source = readback.GetRawTextureData<byte>();
                var pixels = new byte[checked(texture.width * texture.height * 4)];
                NativeArray<byte>.Copy(source, pixels, pixels.Length);
                return pixels;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null)
                {
                    RenderTexture.ReleaseTemporary(temporary);
                }

                if (readback != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(readback);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(readback);
                    }
                }
            }
        }
    }
}
#endif
