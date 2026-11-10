#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Collections.Generic;
using InteropMarshal = System.Runtime.InteropServices.Marshal;
using UnityEngine;

namespace ByteDance.PICO.XR
{
    internal interface IUnmanagedAllocator
    {
        IntPtr Allocate(int byteCount);

        void Free(IntPtr allocation);
    }

    internal sealed class MarshalUnmanagedAllocator : IUnmanagedAllocator
    {
        internal static MarshalUnmanagedAllocator Instance { get; } =
            new MarshalUnmanagedAllocator();

        private MarshalUnmanagedAllocator()
        {
        }

        public IntPtr Allocate(int byteCount)
        {
            return InteropMarshal.AllocHGlobal(byteCount);
        }

        public void Free(IntPtr allocation)
        {
            InteropMarshal.FreeHGlobal(allocation);
        }
    }

    internal readonly struct ImageReferencePlaneInput
    {
        internal ImageReferencePlaneInput(
            byte[] buffer,
            uint rowStride,
            uint pixelStride)
        {
            Buffer = buffer;
            RowStride = rowStride;
            PixelStride = pixelStride;
        }

        internal byte[] Buffer { get; }

        internal uint RowStride { get; }

        internal uint PixelStride { get; }
    }

    internal readonly struct ImageReferenceInput
    {
        internal ImageReferenceInput(
            uint width,
            uint height,
            XrSpatialReferenceImageFormat format,
            ImageReferencePlaneInput[] planes,
            float physicalWidth,
            bool optimizeForStaticImage)
        {
            Width = width;
            Height = height;
            Format = format;
            Planes = planes;
            PhysicalWidth = physicalWidth;
            OptimizeForStaticImage = optimizeForStaticImage;
        }

        internal uint Width { get; }

        internal uint Height { get; }

        internal XrSpatialReferenceImageFormat Format { get; }

        internal ImageReferencePlaneInput[] Planes { get; }

        internal float PhysicalWidth { get; }

        internal bool OptimizeForStaticImage { get; }
    }

    internal sealed class MarshalledImageDatabaseInput : IDisposable
    {
        private readonly IUnmanagedAllocator m_Allocator;
        private readonly IList<IntPtr> m_Allocations;
        private bool m_IsDisposed;

        internal MarshalledImageDatabaseInput(IUnmanagedAllocator allocator)
            : this(allocator, new List<IntPtr>())
        {
        }

        internal MarshalledImageDatabaseInput(
            IUnmanagedAllocator allocator,
            IList<IntPtr> allocations)
        {
            m_Allocator = allocator;
            m_Allocations = allocations;
        }

        internal XrSpatialImageTrackingDatabaseCreateInfo CreateInfo { get; private set; }

        internal IntPtr Allocate(int byteCount)
        {
            if (m_IsDisposed)
            {
                throw new ObjectDisposedException(nameof(MarshalledImageDatabaseInput));
            }

            IntPtr allocation = m_Allocator.Allocate(byteCount);
            RegisterAllocation(allocation);
            return allocation;
        }

        private void RegisterAllocation(IntPtr allocation)
        {
            try
            {
                m_Allocations.Add(allocation);
            }
            catch
            {
                m_Allocator.Free(allocation);
                throw;
            }
        }

        internal void SetCreateInfo(XrSpatialImageTrackingDatabaseCreateInfo createInfo)
        {
            CreateInfo = createInfo;
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            m_IsDisposed = true;
            for (int index = m_Allocations.Count - 1; index >= 0; index--)
            {
                m_Allocator.Free(m_Allocations[index]);
            }

            m_Allocations.Clear();
            CreateInfo = default;
        }
    }

    internal static class ImageReferenceMarshaller
    {
        internal static ImageReferenceInput CreateInput(
            ImageTrackingReferenceRecord record,
            XrSpatialReferenceImageFormat format,
            bool optimizeForStaticImage)
        {
            byte[] rgba = record.CanonicalRgba
                ?? throw new ArgumentException(
                    "Canonical RGBA pixels are required.",
                    nameof(record));
            uint width = checked((uint)record.PixelWidth);
            uint height = checked((uint)record.PixelHeight);
            ImageReferencePlaneInput[] planes;
            switch (format)
            {
                case XrSpatialReferenceImageFormat.Rgba8888:
                    planes = new[]
                    {
                        new ImageReferencePlaneInput(
                            rgba,
                            checked(width * 4),
                            4)
                    };
                    break;
                case XrSpatialReferenceImageFormat.Rgb888:
                    planes = new[]
                    {
                        new ImageReferencePlaneInput(
                            ConvertRgbaToRgb(rgba, width, height),
                            checked(width * 3),
                            3)
                    };
                    break;
                case XrSpatialReferenceImageFormat.Yuv420888:
                    planes = ConvertRgbaToYuv420(rgba, width, height);
                    break;
                default:
                    throw new NotSupportedException(
                        $"Unsupported reference image format: {format}.");
            }

            return new ImageReferenceInput(
                width,
                height,
                format,
                planes,
                record.PhysicalWidth,
                optimizeForStaticImage);
        }

        internal static MarshalledImageDatabaseInput Marshal(
            ImageReferenceInput input,
            IUnmanagedAllocator allocator = null)
        {
            return Marshal(new[] { input }, allocator);
        }

        internal static MarshalledImageDatabaseInput Marshal(
            IReadOnlyList<ImageReferenceInput> inputs,
            IUnmanagedAllocator allocator = null)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            if (inputs.Count == 0)
            {
                throw new ArgumentException(
                    "At least one reference image is required.",
                    nameof(inputs));
            }

            var owner = new MarshalledImageDatabaseInput(
                allocator ?? MarshalUnmanagedAllocator.Instance);
            try
            {
                var nativeImages = new XrSpatialReferenceImage[inputs.Count];
                for (int imageIndex = 0; imageIndex < inputs.Count; imageIndex++)
                {
                    nativeImages[imageIndex] = MarshalImage(inputs[imageIndex], owner);
                }

                int imageSize = InteropMarshal.SizeOf<XrSpatialReferenceImage>();
                IntPtr images = owner.Allocate(checked(imageSize * nativeImages.Length));
                for (int imageIndex = 0; imageIndex < nativeImages.Length; imageIndex++)
                {
                    InteropMarshal.StructureToPtr(
                        nativeImages[imageIndex],
                        IntPtr.Add(images, checked(imageIndex * imageSize)),
                        false);
                }

                owner.SetCreateInfo(new XrSpatialImageTrackingDatabaseCreateInfo
                {
                    type = XrStructureType.XR_TYPE_SPATIAL_IMAGE_TRACKING_DATABASE_CREATE_INFO_PICO,
                    spatialReferenceImageCount = checked((uint)nativeImages.Length),
                    spatialReferenceImages = images
                });
                return owner;
            }
            catch
            {
                owner.Dispose();
                throw;
            }
        }

        internal static MarshalledImageDatabaseInput Marshal(
            Texture2D texture,
            float physicalWidth,
            bool optimizeForStaticImage,
            IUnmanagedAllocator allocator = null)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            if (texture.format != TextureFormat.RGBA32)
            {
                throw new NotSupportedException(
                    "Texture2D marshalling supports only readable RGBA32 textures.");
            }

            var baseMipData = texture.GetPixelData<byte>(0);
            var bytes = new byte[baseMipData.Length];
            baseMipData.CopyTo(bytes);

            return Marshal(
                new ImageReferenceInput(
                    checked((uint)texture.width),
                    checked((uint)texture.height),
                    XrSpatialReferenceImageFormat.Rgba8888,
                    new[]
                    {
                        new ImageReferencePlaneInput(
                            bytes,
                            checked((uint)texture.width * 4),
                            4)
                    },
                    physicalWidth,
                    optimizeForStaticImage),
                allocator);
        }

        private static XrSpatialReferenceImage MarshalImage(
            ImageReferenceInput input,
            MarshalledImageDatabaseInput owner)
        {
            Validate(input);

            var nativePlanes = new XrSpatialReferenceImagePlane[input.Planes.Length];
            for (int planeIndex = 0; planeIndex < input.Planes.Length; planeIndex++)
            {
                ImageReferencePlaneInput plane = input.Planes[planeIndex];
                uint planeHeight = GetPlaneHeight(input, planeIndex);
                byte[] canonicalBuffer = FlipVertically(
                    plane.Buffer,
                    plane.RowStride,
                    GetPlaneDataByteCount(input, planeIndex),
                    planeHeight);
                IntPtr buffer = owner.Allocate(canonicalBuffer.Length);
                InteropMarshal.Copy(canonicalBuffer, 0, buffer, canonicalBuffer.Length);

                nativePlanes[planeIndex] = new XrSpatialReferenceImagePlane
                {
                    bufferSize = checked((uint)canonicalBuffer.Length),
                    buffer = buffer,
                    rowStride = plane.RowStride,
                    pixelStride = plane.PixelStride
                };
            }

            int planeSize = InteropMarshal.SizeOf<XrSpatialReferenceImagePlane>();
            IntPtr planes = owner.Allocate(checked(planeSize * nativePlanes.Length));
            for (int planeIndex = 0; planeIndex < nativePlanes.Length; planeIndex++)
            {
                InteropMarshal.StructureToPtr(
                    nativePlanes[planeIndex],
                    IntPtr.Add(planes, checked(planeIndex * planeSize)),
                    false);
            }

            return new XrSpatialReferenceImage
            {
                type = XrStructureType.XR_TYPE_SPATIAL_REFERENCE_IMAGE_PICO,
                width = input.Width,
                height = input.Height,
                format = input.Format,
                planeCount = checked((uint)nativePlanes.Length),
                planes = planes,
                imageSize = new XrSpatialImageSize
                {
                    physicalWidth = input.PhysicalWidth
                },
                staticOptimization = new XrSpatialImageStaticOptimization
                {
                    optimizeForStaticImage = input.OptimizeForStaticImage ? 1u : 0u
                }
            };
        }

        private static byte[] ConvertRgbaToRgb(
            byte[] rgba,
            uint width,
            uint height)
        {
            int pixelCount = checked((int)(width * height));
            var rgb = new byte[checked(pixelCount * 3)];
            for (int pixel = 0; pixel < pixelCount; pixel++)
            {
                int sourceOffset = pixel * 4;
                int destinationOffset = pixel * 3;
                rgb[destinationOffset] = rgba[sourceOffset];
                rgb[destinationOffset + 1] = rgba[sourceOffset + 1];
                rgb[destinationOffset + 2] = rgba[sourceOffset + 2];
            }

            return rgb;
        }

        private static ImageReferencePlaneInput[] ConvertRgbaToYuv420(
            byte[] rgba,
            uint width,
            uint height)
        {
            int imageWidth = checked((int)width);
            int imageHeight = checked((int)height);
            int chromaWidth = (imageWidth + 1) / 2;
            int chromaHeight = (imageHeight + 1) / 2;
            var y = new byte[checked(imageWidth * imageHeight)];
            var u = new byte[checked(chromaWidth * chromaHeight)];
            var v = new byte[checked(chromaWidth * chromaHeight)];

            for (int pixelY = 0; pixelY < imageHeight; pixelY++)
            {
                for (int pixelX = 0; pixelX < imageWidth; pixelX++)
                {
                    int sourceOffset =
                        checked((pixelY * imageWidth + pixelX) * 4);
                    int red = rgba[sourceOffset];
                    int green = rgba[sourceOffset + 1];
                    int blue = rgba[sourceOffset + 2];
                    y[pixelY * imageWidth + pixelX] =
                        ClampToByte((77 * red + 150 * green + 29 * blue + 128) >> 8);
                }
            }

            for (int chromaY = 0; chromaY < chromaHeight; chromaY++)
            {
                for (int chromaX = 0; chromaX < chromaWidth; chromaX++)
                {
                    int redTotal = 0;
                    int greenTotal = 0;
                    int blueTotal = 0;
                    int sampleCount = 0;
                    for (int offsetY = 0; offsetY < 2; offsetY++)
                    {
                        int pixelY = chromaY * 2 + offsetY;
                        if (pixelY >= imageHeight)
                        {
                            continue;
                        }

                        for (int offsetX = 0; offsetX < 2; offsetX++)
                        {
                            int pixelX = chromaX * 2 + offsetX;
                            if (pixelX >= imageWidth)
                            {
                                continue;
                            }

                            int sourceOffset =
                                checked((pixelY * imageWidth + pixelX) * 4);
                            redTotal += rgba[sourceOffset];
                            greenTotal += rgba[sourceOffset + 1];
                            blueTotal += rgba[sourceOffset + 2];
                            sampleCount++;
                        }
                    }

                    int red = redTotal / sampleCount;
                    int green = greenTotal / sampleCount;
                    int blue = blueTotal / sampleCount;
                    int chromaIndex = chromaY * chromaWidth + chromaX;
                    u[chromaIndex] = ClampToByte(
                        ((-43 * red - 85 * green + 128 * blue + 128) >> 8)
                        + 128);
                    v[chromaIndex] = ClampToByte(
                        ((128 * red - 107 * green - 21 * blue + 128) >> 8)
                        + 128);
                }
            }

            return new[]
            {
                new ImageReferencePlaneInput(y, width, 1),
                new ImageReferencePlaneInput(
                    u,
                    checked((uint)chromaWidth),
                    1),
                new ImageReferencePlaneInput(
                    v,
                    checked((uint)chromaWidth),
                    1)
            };
        }

        private static byte ClampToByte(int value)
        {
            return (byte)Math.Max(byte.MinValue, Math.Min(byte.MaxValue, value));
        }

        private static void Validate(ImageReferenceInput input)
        {
            if (input.Width == 0 || input.Height == 0)
            {
                throw new ArgumentException("Reference image dimensions must be positive.");
            }

            if (input.PhysicalWidth <= 0f
                || float.IsNaN(input.PhysicalWidth)
                || float.IsInfinity(input.PhysicalWidth))
            {
                throw new ArgumentException("Physical width must be finite and positive.");
            }

            int expectedPlaneCount;
            switch (input.Format)
            {
                case XrSpatialReferenceImageFormat.Rgba8888:
                case XrSpatialReferenceImageFormat.Rgb888:
                    expectedPlaneCount = 1;
                    break;
                case XrSpatialReferenceImageFormat.Yuv420888:
                    expectedPlaneCount = 3;
                    break;
                default:
                    throw new NotSupportedException(
                        $"Unsupported reference image format: {input.Format}.");
            }

            if (input.Planes == null || input.Planes.Length != expectedPlaneCount)
            {
                throw new ArgumentException(
                    $"{input.Format} requires {expectedPlaneCount} plane(s).");
            }

            for (int planeIndex = 0; planeIndex < input.Planes.Length; planeIndex++)
            {
                ValidatePlane(input, planeIndex);
            }
        }

        private static void ValidatePlane(ImageReferenceInput input, int planeIndex)
        {
            ImageReferencePlaneInput plane = input.Planes[planeIndex];
            if (plane.Buffer == null)
            {
                throw new ArgumentException($"Plane {planeIndex} has no buffer.");
            }

            uint expectedPixelStride;
            uint expectedRowStride;
            if (input.Format == XrSpatialReferenceImageFormat.Rgba8888)
            {
                expectedPixelStride = 4;
                expectedRowStride = checked(input.Width * expectedPixelStride);
            }
            else if (input.Format == XrSpatialReferenceImageFormat.Rgb888)
            {
                expectedPixelStride = 3;
                expectedRowStride = checked(input.Width * expectedPixelStride);
            }
            else
            {
                expectedPixelStride = plane.PixelStride;
                expectedRowStride = plane.RowStride;
            }

            if (plane.PixelStride == 0
                || plane.RowStride == 0
                || plane.PixelStride != expectedPixelStride
                || plane.RowStride != expectedRowStride)
            {
                throw new ArgumentException($"Plane {planeIndex} has invalid strides.");
            }

            uint planeWidth = GetPlaneWidth(input, planeIndex);
            ulong minimumRowBytes =
                ((ulong)planeWidth - 1UL) * plane.PixelStride + 1UL;
            if (plane.RowStride < minimumRowBytes)
            {
                throw new ArgumentException(
                    $"Plane {planeIndex} row stride is too small.");
            }

            uint planeHeight = GetPlaneHeight(input, planeIndex);
            ulong planeDataByteCount = GetPlaneDataByteCount(input, planeIndex);
            int minimumBufferSize = checked((int)(
                ((ulong)planeHeight - 1UL) * plane.RowStride
                + planeDataByteCount));
            if (plane.Buffer.Length < minimumBufferSize)
            {
                throw new ArgumentException(
                    $"Plane {planeIndex} buffer is too small for its dimensions and strides.");
            }
        }

        private static uint GetPlaneDataByteCount(
            ImageReferenceInput input,
            int planeIndex)
        {
            uint planeWidth = GetPlaneWidth(input, planeIndex);
            ImageReferencePlaneInput plane = input.Planes[planeIndex];
            if (input.Format == XrSpatialReferenceImageFormat.Rgba8888
                || input.Format == XrSpatialReferenceImageFormat.Rgb888)
            {
                return checked(planeWidth * plane.PixelStride);
            }

            return checked((planeWidth - 1) * plane.PixelStride + 1);
        }

        private static uint GetPlaneWidth(ImageReferenceInput input, int planeIndex)
        {
            return input.Format == XrSpatialReferenceImageFormat.Yuv420888
                   && planeIndex > 0
                ? input.Width / 2 + input.Width % 2
                : input.Width;
        }

        private static uint GetPlaneHeight(ImageReferenceInput input, int planeIndex)
        {
            return input.Format == XrSpatialReferenceImageFormat.Yuv420888
                   && planeIndex > 0
                ? input.Height / 2 + input.Height % 2
                : input.Height;
        }

        private static byte[] FlipVertically(
            byte[] source,
            uint rowStride,
            uint rowDataByteCount,
            uint rowCount)
        {
            int rowByteCount = checked((int)rowStride);
            int dataByteCount = checked((int)rowDataByteCount);
            int rows = checked((int)rowCount);
            var destination = new byte[checked(rowByteCount * rows)];
            for (int destinationRow = 0; destinationRow < rows; destinationRow++)
            {
                int sourceOffset = checked((rows - destinationRow - 1) * rowByteCount);
                int destinationOffset = checked(destinationRow * rowByteCount);
                int availableByteCount = source.Length - sourceOffset;
                Buffer.BlockCopy(
                    source,
                    sourceOffset,
                    destination,
                    destinationOffset,
                    Math.Min(dataByteCount, availableByteCount));
            }

            return destination;
        }
    }
}
#endif
