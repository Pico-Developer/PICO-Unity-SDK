#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    internal readonly struct ImageTrackingReferenceRecord
    {
        private readonly bool m_HasTexture;

        private ImageTrackingReferenceRecord(
            Guid guid,
            string name,
            byte[] canonicalRgba,
            int pixelWidth,
            int pixelHeight,
            float physicalWidth,
            bool hasTexture,
            string captureError)
        {
            Guid = guid;
            Name = name;
            CanonicalRgba = canonicalRgba;
            PixelWidth = pixelWidth;
            PixelHeight = pixelHeight;
            PhysicalWidth = physicalWidth;
            m_HasTexture = hasTexture;
            CaptureError = captureError;
        }

        internal Guid Guid { get; }

        internal string Name { get; }

        internal byte[] CanonicalRgba { get; }

        internal int PixelWidth { get; }

        internal int PixelHeight { get; }

        internal float PhysicalWidth { get; }

        private string CaptureError { get; }

        internal static ImageTrackingReferenceRecord CreateForTest(
            Guid guid,
            string name,
            int pixelWidth,
            int pixelHeight,
            float physicalWidth,
            bool hasTexture = true)
        {
            return new ImageTrackingReferenceRecord(
                guid,
                name,
                hasTexture && pixelWidth > 0 && pixelHeight > 0
                    ? new byte[checked(pixelWidth * pixelHeight * 4)]
                    : null,
                pixelWidth,
                pixelHeight,
                physicalWidth,
                hasTexture,
                string.Empty);
        }

        internal ValidationResult Validate()
        {
            return Validate(ImageTrackingCapabilitySet.V1);
        }

        internal ValidationResult Validate(ImageTrackingCapabilitySet capabilities)
        {
            ImageTrackingLog.Verbose($"Validate image name={Name} guid={Guid} hasTexture={m_HasTexture} pixel={PixelWidth}x{PixelHeight} physicalWidth={PhysicalWidth} caps=[{capabilities.MinimumImageDimension},{capabilities.MaximumImageDimension}]");
            if (!m_HasTexture)
            {
                return new ValidationResult(
                    ImageTrackingConfigureStatus.InvalidReferenceImage,
                    "Keep Texture at Runtime must be enabled for every reference image.");
            }

            if (CanonicalRgba == null
                || CanonicalRgba.Length
                   != checked(PixelWidth * PixelHeight * 4))
            {
                return new ValidationResult(
                    ImageTrackingConfigureStatus.InvalidReferenceImage,
                    string.IsNullOrEmpty(CaptureError)
                        ? "The reference image could not be converted to canonical RGBA pixels."
                        : CaptureError);
            }

            if (PhysicalWidth <= 0f || float.IsNaN(PhysicalWidth) || float.IsInfinity(PhysicalWidth))
            {
                return new ValidationResult(
                    ImageTrackingConfigureStatus.SizeMissing,
                    "The reference image must have a positive physical width.");
            }

            if (!capabilities.SupportsDimensions(PixelWidth, PixelHeight))
            {
                return new ValidationResult(
                    ImageTrackingConfigureStatus.InvalidReferenceImage,
                    "The reference image resolution must be less than or equal to 640x640 pixels.");
            }

            if (capabilities.RequiresDimensionWarning(PixelWidth, PixelHeight))
            {
                return ValidationResult.SuccessWithWarning(
                    "Reference image dimensions below 300 pixels may reduce tracking quality.");
            }

            return ValidationResult.Success;
        }

        internal static ImageTrackingReferenceRecord FromReferenceImage(XRReferenceImage referenceImage)
        {
            Texture2D texture = referenceImage.texture;
            byte[] canonicalRgba = null;
            string captureError = string.Empty;
            if (texture != null)
            {
                try
                {
                    canonicalRgba =
                        ImageReferenceTextureCapture.CaptureCanonicalRgba(texture);
                }
                catch (Exception exception)
                {
                    captureError =
                        "The reference image could not be converted to canonical "
                        + $"RGBA pixels: {exception.Message}";
                }
            }

            return new ImageTrackingReferenceRecord(
                referenceImage.guid,
                referenceImage.name,
                canonicalRgba,
                texture == null ? 0 : texture.width,
                texture == null ? 0 : texture.height,
                referenceImage.specifySize ? referenceImage.width : 0f,
                texture != null,
                captureError);
        }

        internal readonly struct ValidationResult
        {
            internal ValidationResult(ImageTrackingConfigureStatus status, string message)
                : this(status, message, string.Empty)
            {
            }

            private ValidationResult(
                ImageTrackingConfigureStatus status,
                string message,
                string warning)
            {
                Status = status;
                Message = message;
                Warning = warning;
            }

            internal static ValidationResult Success { get; } =
                new ValidationResult(ImageTrackingConfigureStatus.Success, string.Empty);

            internal static ValidationResult SuccessWithWarning(string warning)
            {
                return new ValidationResult(
                    ImageTrackingConfigureStatus.Success,
                    string.Empty,
                    warning);
            }

            internal ImageTrackingConfigureStatus Status { get; }

            internal string Message { get; }

            internal string Warning { get; }
        }
    }

    internal sealed class ImageTrackingRuntimeLibrary
    {
        private readonly ImageTrackingReferenceRecord[] m_Records;

        private ImageTrackingRuntimeLibrary(
            Guid sourceGuid,
            ImageTrackingReferenceRecord[] records,
            ImageTrackingReferenceRecord.ValidationResult validationReport)
        {
            SourceGuid = sourceGuid;
            m_Records = records;
            ValidationReport = validationReport;
        }

        internal Guid SourceGuid { get; }

        internal int Count => m_Records.Length;

        internal bool IsValid => ValidationReport.Status == ImageTrackingConfigureStatus.Success;

        internal ImageTrackingReferenceRecord.ValidationResult ValidationReport { get; }

        internal ImageTrackingReferenceRecord this[int index] => m_Records[index];

        internal static ImageTrackingRuntimeLibrary Create(
            XRReferenceImageLibrary source,
            ImageTrackingCapabilitySet capabilities)
        {
            ImageTrackingLog.Verbose(() => $"RuntimeLibrary.Create sourceNull={source == null} count={source?.count ?? -1} sourceGuid={(source == null ? Guid.Empty : source.guid)} caps min={capabilities.MinimumImageDimension} max={capabilities.MaximumImageDimension} maxRefs={capabilities.MaximumReferenceImageCount}");
            if (source == null)
            {
                return Invalid(
                    Guid.Empty,
                    Array.Empty<ImageTrackingReferenceRecord>(),
                    ImageTrackingConfigureStatus.InvalidLibrary,
                    "The source image library is null.");
            }

            int sourceCount = source.count;
            if (sourceCount == 0)
            {
                return Invalid(
                    source.guid,
                    Array.Empty<ImageTrackingReferenceRecord>(),
                    ImageTrackingConfigureStatus.InvalidLibrary,
                    "The source image library is empty.");
            }

            if (sourceCount >= capabilities.MaximumReferenceImageCount)
            {
                return Invalid(
                    source.guid,
                    Array.Empty<ImageTrackingReferenceRecord>(),
                    ImageTrackingConfigureStatus.LimitReached,
                    "The source image library count must satisfy 0 < count < 300.");
            }

            var records = new ImageTrackingReferenceRecord[sourceCount];
            for (int index = 0; index < sourceCount; index++)
            {
                records[index] = ImageTrackingReferenceRecord.FromReferenceImage(source[index]);
                ImageTrackingLog.Verbose(() => $"RuntimeLibrary.Record[{index}] name={records[index].Name} guid={records[index].Guid} pixel={records[index].PixelWidth}x{records[index].PixelHeight} physicalWidth={records[index].PhysicalWidth}");
            }

            var guids = new HashSet<Guid>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            ImageTrackingReferenceRecord.ValidationResult validationReport =
                ImageTrackingReferenceRecord.ValidationResult.Success;
            foreach (ImageTrackingReferenceRecord record in records)
            {
                ImageTrackingReferenceRecord.ValidationResult validation =
                    record.Validate(capabilities);
                if (validation.Status != ImageTrackingConfigureStatus.Success)
                {
                    return new ImageTrackingRuntimeLibrary(source.guid, records, validation);
                }

                if (!string.IsNullOrEmpty(validation.Warning)
                    && string.IsNullOrEmpty(validationReport.Warning))
                {
                    validationReport = validation;
                }

                if (!string.IsNullOrEmpty(validation.Warning))
                {
                    LogValidationWarning(record, validation.Warning);
                }

                if (!guids.Add(record.Guid))
                {
                    const string warning =
                        "The source image library contains a duplicate reference image GUID. "
                        + "Runtime image mapping may be ambiguous.";
                    LogValidationWarning(record, warning);
                    if (string.IsNullOrEmpty(validationReport.Warning))
                    {
                        validationReport =
                            ImageTrackingReferenceRecord.ValidationResult
                                .SuccessWithWarning(warning);
                    }
                }

                if (!names.Add(record.Name))
                {
                    const string warning =
                        "The source image library contains duplicate reference image names. "
                        + "Use reference image GUIDs for unique mapping.";
                    LogValidationWarning(record, warning);
                    if (string.IsNullOrEmpty(validationReport.Warning))
                    {
                        validationReport =
                            ImageTrackingReferenceRecord.ValidationResult
                                .SuccessWithWarning(warning);
                    }
                }
            }

            return new ImageTrackingRuntimeLibrary(
                source.guid,
                records,
                validationReport);
        }

        private static ImageTrackingRuntimeLibrary Invalid(
            Guid sourceGuid,
            ImageTrackingReferenceRecord[] records,
            ImageTrackingConfigureStatus status,
            string message)
        {
            return new ImageTrackingRuntimeLibrary(
                sourceGuid,
                records,
                new ImageTrackingReferenceRecord.ValidationResult(status, message));
        }

        private static void LogValidationWarning(
            ImageTrackingReferenceRecord record,
            string warning)
        {
            Debug.LogWarning(
                $"{ImageTrackingLog.Tag} Reference image '{record.Name}' "
                + $"({record.Guid}): {warning}");
        }
    }
}
#endif
