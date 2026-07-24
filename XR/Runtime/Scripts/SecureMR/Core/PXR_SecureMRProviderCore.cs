#if !ENABLE_PICO_OPENXR_SDK
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ByteDance.PICO.XR;

namespace ByteDance.PICO.SecureMR
{
    public class Provider
    {
        private readonly ISecureMRBackend backend;
        private ulong providerHandle;

        public Provider(int width,int height)
        {
            backend = SecureMRBackendRouter.Current;
            providerHandle = backend.CreateProvider(width, height);
        }

        public Provider(int width, int height, int containerWidth, int containerHeight, int containerDepth)
        {
            backend = SecureMRBackendRouter.Current;
            if (backend is SecureMRBackendSpatial spatialBackend)
            {
                providerHandle = spatialBackend.CreateProvider(width, height, containerWidth, containerHeight, containerDepth);
                return;
            }

            providerHandle = backend.CreateProvider(width, height);
        }

        public Pipeline CreatePipeline()
        {
            return new Pipeline(backend, providerHandle);
        }


        public Tensor CreateTensor<T, TType>(int channels, TensorShape shape, T[] data = null)
            where T : struct
            where TType : TensorBase, new()
        {
            PXR_SecureMRPlugin.TensorDataTypeToEnum.TryGetValue(typeof(T), out var dataType);
            PXR_SecureMRPlugin.TensorClassToEnum.TryGetValue(typeof(TType), out var enumValue);

            if (enumValue == SecureMRTensorUsage.DynamicTexture)
            {
                if (dataType == SecureMRTensorDataType.Byte)
                {
                    dataType = SecureMRTensorDataType.DynamicTextureByte;
                }

                if (dataType == SecureMRTensorDataType.Float)
                {
                    dataType = SecureMRTensorDataType.DynamicTextureFloat;
                }
            }
            
            var tensorHandle = backend.CreateGlobalTensorByShape(providerHandle, dataType, shape.Dimensions, (sbyte)channels, enumValue);
            var t = new Tensor(tensorHandle, 0, false, true)
            {
                Dimensions = shape.Dimensions,
                Channels = (sbyte)channels,
                Usage = enumValue,
                DataType = dataType
            };
            t.Backend = backend;
            if (data != null)
            {
                t.Reset(data);
            }
            return t;
            
        }
        
        public Tensor CreateTensor<TType>(byte[] data)
            where TType : Gltf, new()
        {
            var tensorHandle = backend.CreateGlobalTensorByGltf(providerHandle, data);
            var t = new Tensor(tensorHandle, 0, false, true);
            t.Backend = backend;
            return t;
        }
        
        public void Destroy()
        {
            if (providerHandle == 0) return;
            try
            {
                backend.DestroyProvider(providerHandle);
            }
            catch
            {
            }
            finally
            {
                providerHandle = 0;
            }
        }

        ~Provider()
        {
            Destroy();
        }
    }

}
#endif
