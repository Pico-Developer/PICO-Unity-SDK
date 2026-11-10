#if AR_FOUNDATION_6_IMAGE_TRACKING
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ByteDance.PICO.XR
{
    internal sealed class ImageTrackingChangeAssembler
    {
        private const string DefaultBackendId = "PICO";

        private readonly object m_Gate = new object();
        private readonly string m_BackendId;
        private readonly Dictionary<ulong, XRTrackedImage> m_TrackedImages =
            new Dictionary<ulong, XRTrackedImage>();
        private readonly List<TrackableId> m_PendingRemovals =
            new List<TrackableId>();

        private Guid m_DatabaseId;

        internal ImageTrackingChangeAssembler()
            : this(DefaultBackendId, Guid.Empty)
        {
        }

        internal ImageTrackingChangeAssembler(string backendId, Guid databaseId)
        {
            if (string.IsNullOrEmpty(backendId))
            {
                throw new ArgumentException(
                    "A backend identifier is required.",
                    nameof(backendId));
            }

            m_BackendId = backendId;
            m_DatabaseId = databaseId;
        }

        internal ImageTrackingChanges Assemble(
            IReadOnlyList<ImageTrackingObservation> observations)
        {
            observations = observations
                ?? Array.Empty<ImageTrackingObservation>();

            lock (m_Gate)
            {
                var added = new List<XRTrackedImage>();
                var updated = new List<XRTrackedImage>();
                var observedEntities = new HashSet<ulong>();

                for (int index = 0; index < observations.Count; index++)
                {
                    ImageTrackingObservation observation = observations[index];
                    if (!observedEntities.Add(observation.EntityId))
                    {
                        continue;
                    }

                    if (m_TrackedImages.TryGetValue(
                            observation.EntityId,
                            out XRTrackedImage previous))
                    {
                        XRTrackedImage next =
                            observation.TrackingState == TrackingState.None
                                ? WithTrackingState(previous, TrackingState.Limited)
                                : CreateTrackedImage(
                                    previous.trackableId,
                                    observation,
                                    observation.TrackingState);
                        m_TrackedImages[observation.EntityId] = next;
                        UpsertMetadata(next.trackableId, observation);
                        updated.Add(next);
                    }
                    else
                    {
                        TrackingState trackingState =
                            observation.TrackingState == TrackingState.None
                                ? TrackingState.Limited
                                : observation.TrackingState;
                        XRTrackedImage next = CreateTrackedImage(
                            CreateTrackableId(observation.EntityId),
                            observation,
                            trackingState);
                        m_TrackedImages.Add(observation.EntityId, next);
                        UpsertMetadata(next.trackableId, observation);
                        added.Add(next);
                    }
                }

                var trackedEntities =
                    new List<ulong>(m_TrackedImages.Keys);
                foreach (ulong entityId in trackedEntities)
                {
                    if (observedEntities.Contains(entityId))
                    {
                        continue;
                    }

                    XRTrackedImage trackedImage = m_TrackedImages[entityId];
                    if (trackedImage.trackingState == TrackingState.Limited)
                    {
                        // Already reported as Limited on the transition frame;
                        // keep the frozen entry but do not re-emit an identical
                        // Updated every pump until the module evicts it.
                        continue;
                    }

                    XRTrackedImage limited = WithTrackingState(
                        trackedImage,
                        TrackingState.Limited);
                    m_TrackedImages[entityId] = limited;
                    updated.Add(limited);
                }

                TrackableId[] removed = m_PendingRemovals.ToArray();
                m_PendingRemovals.Clear();
                return new ImageTrackingChanges(
                    added.ToArray(),
                    updated.ToArray(),
                    removed);
            }
        }

        internal void ReplaceDatabase(Guid databaseId)
        {
            lock (m_Gate)
            {
                if (databaseId == m_DatabaseId)
                {
                    return;
                }

                QueueAllRemovals();
                m_DatabaseId = databaseId;
            }
        }

        internal void RemoveEntity(ulong entityId)
        {
            lock (m_Gate)
            {
                if (m_TrackedImages.TryGetValue(
                        entityId,
                        out XRTrackedImage trackedImage))
                {
                    m_TrackedImages.Remove(entityId);
                    PXRImageTrackingMetadataRegistry.Remove(trackedImage.trackableId);
                    m_PendingRemovals.Add(trackedImage.trackableId);
                }
            }
        }

        internal void RemoveAll()
        {
            lock (m_Gate)
            {
                QueueAllRemovals();
            }
        }

        private void QueueAllRemovals()
        {
            foreach (XRTrackedImage trackedImage in m_TrackedImages.Values)
            {
                m_PendingRemovals.Add(trackedImage.trackableId);
                PXRImageTrackingMetadataRegistry.Remove(trackedImage.trackableId);
            }

            m_TrackedImages.Clear();
        }

        internal bool TryGetMetadata(
            TrackableId trackableId,
            out PXR_TrackedImageMetadata metadata)
        {
            return PXRImageTrackingMetadataRegistry.TryGet(
                trackableId,
                out metadata);
        }

        private static void UpsertMetadata(
            TrackableId trackableId,
            ImageTrackingObservation observation)
        {
            PXRImageTrackingMetadataRegistry.Upsert(
                new PXR_TrackedImageMetadata(
                    trackableId,
                    observation.ReferenceImageGuid,
                    observation.EntityId,
                    observation.ReferenceImageIndex,
                    observation.BoundingBoxOffset,
                    observation.Size,
                    observation.HasBoundingBox2D,
                    observation.HasValidPose,
                    observation.AlignmentMode,
                    observation.IsAligned,
                    observation.HasAlignedPose,
                    observation.ModelScale,
                    observation.EntityLastUpdateTime));
        }

        private XRTrackedImage CreateTrackedImage(
            TrackableId trackableId,
            ImageTrackingObservation observation,
            TrackingState trackingState)
        {
            return new XRTrackedImage(
                trackableId,
                observation.ReferenceImageGuid,
                observation.Pose,
                observation.Size,
                trackingState,
                IntPtr.Zero,
                TrackableId.invalidId);
        }

        private static XRTrackedImage WithTrackingState(
            XRTrackedImage trackedImage,
            TrackingState trackingState)
        {
            return new XRTrackedImage(
                trackedImage.trackableId,
                trackedImage.sourceImageId,
                trackedImage.pose,
                trackedImage.size,
                trackingState,
                IntPtr.Zero,
                trackedImage.parentId);
        }

        private TrackableId CreateTrackableId(ulong entityId)
        {
            byte[] backendBytes = Encoding.UTF8.GetBytes(m_BackendId);
            byte[] databaseBytes = m_DatabaseId.ToByteArray();
            byte[] entityBytes = BitConverter.GetBytes(entityId);
            byte[] namespacedIdentity = new byte[
                sizeof(int)
                + backendBytes.Length
                + databaseBytes.Length
                + entityBytes.Length];

            byte[] backendLength = BitConverter.GetBytes(backendBytes.Length);
            int offset = 0;
            Buffer.BlockCopy(
                backendLength,
                0,
                namespacedIdentity,
                offset,
                backendLength.Length);
            offset += backendLength.Length;
            Buffer.BlockCopy(
                backendBytes,
                0,
                namespacedIdentity,
                offset,
                backendBytes.Length);
            offset += backendBytes.Length;
            Buffer.BlockCopy(
                databaseBytes,
                0,
                namespacedIdentity,
                offset,
                databaseBytes.Length);
            offset += databaseBytes.Length;
            Buffer.BlockCopy(
                entityBytes,
                0,
                namespacedIdentity,
                offset,
                entityBytes.Length);

            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(namespacedIdentity);
            }

            ulong subId1 = BitConverter.ToUInt64(hash, 0);
            ulong subId2 = BitConverter.ToUInt64(hash, sizeof(ulong));
            if (subId1 == 0 && subId2 == 0)
            {
                subId2 = 1;
            }

            return new TrackableId(subId1, subId2);
        }
    }
}
#endif
