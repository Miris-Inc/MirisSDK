// Copyright © 2026 Miris, Inc. All rights reserved.
using System.Collections.Generic;
using UnityEngine;

namespace Miris.Runtime {

    public static class ComputeKernelUtils 
    {
        // Calculate the thread group count for the specific kernel and data size
        public static (int, int, int) CalculateThreadGroupCount(ComputeShader computeKernel, int kernelIndex, int dataSizeX, int dataSizeY = 1, int dataSizeZ = 1) 
        {
            //Query the thread group size for the x, y and z dimensions from the compute kernel
            computeKernel.GetKernelThreadGroupSizes( kernelIndex, out uint threadsPerGroupX, out uint threadsPerGroupY, out uint threadsPerGroupZ );

            return CalculateThreadGroupCount( (threadsPerGroupX, threadsPerGroupY, threadsPerGroupZ), dataSizeX, dataSizeY, dataSizeZ );
        }

        // Calculate the thread group count given previously determined dimensions
        public static (int, int, int) CalculateThreadGroupCount((uint, uint, uint) threadsPerGroup, int dataSizeX, int dataSizeY = 1, int dataSizeZ = 1)
        {
            // Calculate how many thread groups are needed for each dimension. 
            int threadGroupCountX = GetDimensionThreadGroupCount(dataSizeX, threadsPerGroup.Item1);

            int threadGroupCountY = GetDimensionThreadGroupCount(dataSizeY, threadsPerGroup.Item2);

            int threadGroupCountZ = GetDimensionThreadGroupCount(dataSizeZ, threadsPerGroup.Item3);

            return (threadGroupCountX, threadGroupCountY, threadGroupCountZ);
        }

        // Function ensures that the division rounds up, i.e. it accounts
        // for any remainder that would require and additional thread group
        private static int GetDimensionThreadGroupCount(int dataSize, uint threadsPerGroup) {
            
            return (dataSize + (int)threadsPerGroup - 1) / (int)threadsPerGroup;
            
        }
        
    }
}


