// Copyright © 2026 Miris, Inc. All rights reserved.

// SPDX-License-Identifier: MIT

#ifndef SPHERICAL_HARMONICS_HLSL
#define SPHERICAL_HARMONICS_HLSL

// SH rotation based on https://github.com/andrewwillmott/sh-lib (Unlicense / public domain)
// Original implementation: https://github.com/aras-p/UnityGaussianSplatting/blob/main/package/Shaders/SphericalHarmonics.hlsl
// Changed variables/functions names so that is clearer to understand and not so cryptic. Added comments to understand what is going on.

#define SH_MAX_ORDER 4
#define SH_MAX_COEFFS_COUNT (SH_MAX_ORDER*SH_MAX_ORDER)
#define SH_COEFFS_COUNT 15

// SPHERICAL HARMONICS
static const float SH_C1 = 0.4886025;
static const float SH_C2[] = { 1.0925484, -1.0925484, 0.3153916, -1.0925484, 0.5462742 };
static const float SH_C3[] = { -0.5900436, 2.8906114, -0.4570458, 0.3731763, -0.4570458, 1.4453057, -0.5900436 };

struct SplatSHData
{
    float3 sh[15];
};

// Computes the lighting equation based on Spherical Harmonics
float3 ComputeSH(SplatSHData splat, float3 color, float3 direction, int shOrder, bool onlySH, int shCount)
{

    // Flip the direction vector as we are calculating light contribution
    direction *= -1;
    
    // Extract components of the direction vector
    half dirX = direction.x;
    half dirY = direction.y;
    half dirZ = direction.z;
    
    // Start with the precomputed ambient color (Band 0)
    float3 lightingResult = color; // col = sh0 * SH_C0 + 0.5 is already precomputed
    lightingResult = onlySH ? 0.5 : color; // Optionally override with only the SH component

    int linearBand = (shOrder >= 1) & (shCount >= 3);
    int quadraticBand = (shOrder >= 2) & (shCount >= 8);
    int cubicBand = (shOrder >= 3) & (shCount >= 15);

    lightingResult += (linearBand)?(SH_C1 * (-splat.sh[0] * dirY + splat.sh[1] * dirZ - splat.sh[2] * dirX)):float3(0.0,0.0,0.0);

    // Precompute quadratic terms
    half dirX2 = dirX * dirX;
    half dirY2 = dirY * dirY;
    half dirZ2 = dirZ * dirZ;
    half dirXY = dirX * dirY;
    half dirYZ = dirY * dirZ;
    half dirXZ = dirX * dirZ;
    
    lightingResult +=(quadraticBand)?
        ((SH_C2[0] * dirXY) * splat.sh[3] +
        (SH_C2[1] * dirYZ) * splat.sh[4] +
        (SH_C2[2] * (2 * dirZ2 - dirX2 - dirY2)) * splat.sh[5] +
        (SH_C2[3] * dirXZ) * splat.sh[6] +
        (SH_C2[4] * (dirX2 - dirY2)) * splat.sh[7]):float3(0.0,0.0,0.0);

    lightingResult += (cubicBand)?
                    ((SH_C3[0] * dirY * (3 * dirX2 - dirY2)) * splat.sh[8] +
                    (SH_C3[1] * dirXY * dirZ) * splat.sh[9] +
                    (SH_C3[2] * dirY * (4 * dirZ2 - dirX2 - dirY2)) * splat.sh[10] +
                    (SH_C3[3] * dirZ * (2 * dirZ2 - 3 * dirX2 - 3 * dirY2)) * splat.sh[11] +
                    (SH_C3[4] * dirX * (4 * dirZ2 - dirX2 - dirY2)) * splat.sh[12] +
                    (SH_C3[5] * dirZ * (dirX2 - dirY2)) * splat.sh[13] +
                    (SH_C3[6] * dirX * (dirX2 - 3 * dirY2)) * splat.sh[14]):float3(0.0,0.0,0.0);
    
     return max(lightingResult, 0);
}

float3 ComputedWeightedSumOf3(int startIndex, float3 coefficients[SH_MAX_COEFFS_COUNT], float weights[3])
{
    return coefficients[startIndex+0] * weights[0] + coefficients[startIndex+1] * weights[1] + coefficients[startIndex+2] * weights[2];
}
float3 ComputedWeightedSumOf5(int startIndex, float3 coefficients[SH_MAX_COEFFS_COUNT], float weights[5])
{
    return coefficients[startIndex+0] * weights[0] + coefficients[startIndex+1] * weights[1] + coefficients[startIndex+2] * weights[2] + coefficients[startIndex+3] * weights[3] + coefficients[startIndex+4] * weights[4];
}
float3 ComputedWeightedSumof7(int startIndex, float3 coefficients[SH_MAX_COEFFS_COUNT], float weights[7])
{
    return coefficients[startIndex+0] * weights[0] + coefficients[startIndex+1] * weights[1] + coefficients[startIndex+2] * weights[2] + coefficients[startIndex+3] * weights[3] + coefficients[startIndex+4] * weights[4] + coefficients[startIndex+5] * weights[5] + coefficients[startIndex+6] * weights[6];
}

// Function to rotate SH coefficients to align with a given orientation
void RotateSphericalHarmonics(float3x3 rotationMatrix, int maxBand, float3 inputCoefficients[SH_MAX_COEFFS_COUNT], out float3 outputCoefficients[SH_MAX_COEFFS_COUNT])
{
    // Precomputed constants for SH rotations based on mathematical properties
    const float kSqrt03_02    = sqrt( 3.0 /  2.0);
    const float kSqrt01_03    = sqrt( 1.0 /  3.0);
    const float kSqrt02_03    = sqrt( 2.0 /  3.0);
    const float kSqrt04_03    = sqrt( 4.0 /  3.0);
    const float kSqrt01_04    = sqrt( 1.0 /  4.0);
    const float kSqrt03_04    = sqrt( 3.0 /  4.0);
    const float kSqrt01_05    = sqrt( 1.0 /  5.0);
    const float kSqrt03_05    = sqrt( 3.0 /  5.0);
    const float kSqrt06_05    = sqrt( 6.0 /  5.0);
    const float kSqrt08_05    = sqrt( 8.0 /  5.0);
    const float kSqrt09_05    = sqrt( 9.0 /  5.0);
    const float kSqrt05_06    = sqrt( 5.0 /  6.0);
    const float kSqrt01_06    = sqrt( 1.0 /  6.0);
    const float kSqrt03_08    = sqrt( 3.0 /  8.0);
    const float kSqrt05_08    = sqrt( 5.0 /  8.0);
    const float kSqrt07_08    = sqrt( 7.0 /  8.0);
    const float kSqrt09_08    = sqrt( 9.0 /  8.0);
    const float kSqrt05_09    = sqrt( 5.0 /  9.0);
    const float kSqrt08_09    = sqrt( 8.0 /  9.0);

    const float kSqrt01_10    = sqrt( 1.0 / 10.0);
    const float kSqrt03_10    = sqrt( 3.0 / 10.0);
    const float kSqrt01_12    = sqrt( 1.0 / 12.0);
    const float kSqrt04_15    = sqrt( 4.0 / 15.0);
    const float kSqrt01_16    = sqrt( 1.0 / 16.0);
    const float kSqrt07_16    = sqrt( 7.0 / 16.0);
    const float kSqrt15_16    = sqrt(15.0 / 16.0);
    const float kSqrt01_18    = sqrt( 1.0 / 18.0);
    const float kSqrt03_25    = sqrt( 3.0 / 25.0);
    const float kSqrt14_25    = sqrt(14.0 / 25.0);
    const float kSqrt15_25    = sqrt(15.0 / 25.0);
    const float kSqrt18_25    = sqrt(18.0 / 25.0);
    const float kSqrt01_32    = sqrt( 1.0 / 32.0);
    const float kSqrt03_32    = sqrt( 3.0 / 32.0);
    const float kSqrt15_32    = sqrt(15.0 / 32.0);
    const float kSqrt21_32    = sqrt(21.0 / 32.0);
    const float kSqrt01_50    = sqrt( 1.0 / 50.0);
    const float kSqrt03_50    = sqrt( 3.0 / 50.0);
    const float kSqrt21_50    = sqrt(21.0 / 50.0);

    // Initialize indices for input and output SH coefficients
    int inputIndex = 0;
    int outputIndex = 0;

    // band 0 (constant term) - Represents the average intensity of the environment
    outputCoefficients[outputIndex++] = inputCoefficients[0];
    if (maxBand < 2)
        return;

    // band 1 (linear term) - Represents linear variations in the environment light
    inputIndex += 1;
    float bandRotationMatrix[3][3] =
    {
        // NOTE: change from upstream code at https://github.com/andrewwillmott/sh-lib, some of the
        // values need to have "-" in front of them.

        // Rotational components derived from the orientation matrix
        rotationMatrix._22, -rotationMatrix._23, rotationMatrix._21,
        -rotationMatrix._32, rotationMatrix._33, -rotationMatrix._31,
        rotationMatrix._12, -rotationMatrix._13, rotationMatrix._11
    };
    outputCoefficients[outputIndex++] = ComputedWeightedSumOf3(inputIndex, inputCoefficients, bandRotationMatrix[0]);
    outputCoefficients[outputIndex++] = ComputedWeightedSumOf3(inputIndex, inputCoefficients, bandRotationMatrix[1]);
    outputCoefficients[outputIndex++] = ComputedWeightedSumOf3(inputIndex, inputCoefficients, bandRotationMatrix[2]);
    if (maxBand < 3)
        return;

    // band 2 (quadratic term) - Represents quadratic variations in the environment light
    inputIndex += 3; // Move to the start of band-2 coefficient
    float quadraticRotations[5][5];

    quadraticRotations[0][0] = kSqrt01_04 * ((bandRotationMatrix[2][2] * bandRotationMatrix[0][0] + bandRotationMatrix[2][0] * bandRotationMatrix[0][2]) + (bandRotationMatrix[0][2] * bandRotationMatrix[2][0] + bandRotationMatrix[0][0] * bandRotationMatrix[2][2]));
    quadraticRotations[0][1] = (bandRotationMatrix[2][1] * bandRotationMatrix[0][0] + bandRotationMatrix[0][1] * bandRotationMatrix[2][0]);
    quadraticRotations[0][2] = kSqrt03_04 * (bandRotationMatrix[2][1] * bandRotationMatrix[0][1] + bandRotationMatrix[0][1] * bandRotationMatrix[2][1]);
    quadraticRotations[0][3] = (bandRotationMatrix[2][1] * bandRotationMatrix[0][2] + bandRotationMatrix[0][1] * bandRotationMatrix[2][2]);
    quadraticRotations[0][4] = kSqrt01_04 * ((bandRotationMatrix[2][2] * bandRotationMatrix[0][2] - bandRotationMatrix[2][0] * bandRotationMatrix[0][0]) + (bandRotationMatrix[0][2] * bandRotationMatrix[2][2] - bandRotationMatrix[0][0] * bandRotationMatrix[2][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumOf5(inputIndex, inputCoefficients, quadraticRotations[0]);

    quadraticRotations[1][0] = kSqrt01_04 * ((bandRotationMatrix[1][2] * bandRotationMatrix[0][0] + bandRotationMatrix[1][0] * bandRotationMatrix[0][2]) + (bandRotationMatrix[0][2] * bandRotationMatrix[1][0] + bandRotationMatrix[0][0] * bandRotationMatrix[1][2]));
    quadraticRotations[1][1] = bandRotationMatrix[1][1] * bandRotationMatrix[0][0] + bandRotationMatrix[0][1] * bandRotationMatrix[1][0];
    quadraticRotations[1][2] = kSqrt03_04 * (bandRotationMatrix[1][1] * bandRotationMatrix[0][1] + bandRotationMatrix[0][1] * bandRotationMatrix[1][1]);
    quadraticRotations[1][3] = bandRotationMatrix[1][1] * bandRotationMatrix[0][2] + bandRotationMatrix[0][1] * bandRotationMatrix[1][2];
    quadraticRotations[1][4] = kSqrt01_04 * ((bandRotationMatrix[1][2] * bandRotationMatrix[0][2] - bandRotationMatrix[1][0] * bandRotationMatrix[0][0]) + (bandRotationMatrix[0][2] * bandRotationMatrix[1][2] - bandRotationMatrix[0][0] * bandRotationMatrix[1][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumOf5(inputIndex, inputCoefficients, quadraticRotations[1]);

    quadraticRotations[2][0] = kSqrt01_03 * (bandRotationMatrix[1][2] * bandRotationMatrix[1][0] + bandRotationMatrix[1][0] * bandRotationMatrix[1][2]) + -kSqrt01_12 * ((bandRotationMatrix[2][2] * bandRotationMatrix[2][0] + bandRotationMatrix[2][0] * bandRotationMatrix[2][2]) + (bandRotationMatrix[0][2] * bandRotationMatrix[0][0] + bandRotationMatrix[0][0] * bandRotationMatrix[0][2]));
    quadraticRotations[2][1] = kSqrt04_03 * bandRotationMatrix[1][1] * bandRotationMatrix[1][0] + -kSqrt01_03 * (bandRotationMatrix[2][1] * bandRotationMatrix[2][0] + bandRotationMatrix[0][1] * bandRotationMatrix[0][0]);
    quadraticRotations[2][2] = bandRotationMatrix[1][1] * bandRotationMatrix[1][1] + -kSqrt01_04 * (bandRotationMatrix[2][1] * bandRotationMatrix[2][1] + bandRotationMatrix[0][1] * bandRotationMatrix[0][1]);
    quadraticRotations[2][3] = kSqrt04_03 * bandRotationMatrix[1][1] * bandRotationMatrix[1][2] + -kSqrt01_03 * (bandRotationMatrix[2][1] * bandRotationMatrix[2][2] + bandRotationMatrix[0][1] * bandRotationMatrix[0][2]);
    quadraticRotations[2][4] = kSqrt01_03 * (bandRotationMatrix[1][2] * bandRotationMatrix[1][2] - bandRotationMatrix[1][0] * bandRotationMatrix[1][0]) + -kSqrt01_12 * ((bandRotationMatrix[2][2] * bandRotationMatrix[2][2] - bandRotationMatrix[2][0] * bandRotationMatrix[2][0]) + (bandRotationMatrix[0][2] * bandRotationMatrix[0][2] - bandRotationMatrix[0][0] * bandRotationMatrix[0][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumOf5(inputIndex, inputCoefficients, quadraticRotations[2]);

    quadraticRotations[3][0] = kSqrt01_04 * ((bandRotationMatrix[1][2] * bandRotationMatrix[2][0] + bandRotationMatrix[1][0] * bandRotationMatrix[2][2]) + (bandRotationMatrix[2][2] * bandRotationMatrix[1][0] + bandRotationMatrix[2][0] * bandRotationMatrix[1][2]));
    quadraticRotations[3][1] = bandRotationMatrix[1][1] * bandRotationMatrix[2][0] + bandRotationMatrix[2][1] * bandRotationMatrix[1][0];
    quadraticRotations[3][2] = kSqrt03_04 * (bandRotationMatrix[1][1] * bandRotationMatrix[2][1] + bandRotationMatrix[2][1] * bandRotationMatrix[1][1]);
    quadraticRotations[3][3] = bandRotationMatrix[1][1] * bandRotationMatrix[2][2] + bandRotationMatrix[2][1] * bandRotationMatrix[1][2];
    quadraticRotations[3][4] = kSqrt01_04 * ((bandRotationMatrix[1][2] * bandRotationMatrix[2][2] - bandRotationMatrix[1][0] * bandRotationMatrix[2][0]) + (bandRotationMatrix[2][2] * bandRotationMatrix[1][2] - bandRotationMatrix[2][0] * bandRotationMatrix[1][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumOf5(inputIndex, inputCoefficients, quadraticRotations[3]);

    quadraticRotations[4][0] = kSqrt01_04 * ((bandRotationMatrix[2][2] * bandRotationMatrix[2][0] + bandRotationMatrix[2][0] * bandRotationMatrix[2][2]) - (bandRotationMatrix[0][2] * bandRotationMatrix[0][0] + bandRotationMatrix[0][0] * bandRotationMatrix[0][2]));
    quadraticRotations[4][1] = (bandRotationMatrix[2][1] * bandRotationMatrix[2][0] - bandRotationMatrix[0][1] * bandRotationMatrix[0][0]);
    quadraticRotations[4][2] = kSqrt03_04 * (bandRotationMatrix[2][1] * bandRotationMatrix[2][1] - bandRotationMatrix[0][1] * bandRotationMatrix[0][1]);
    quadraticRotations[4][3] = (bandRotationMatrix[2][1] * bandRotationMatrix[2][2] - bandRotationMatrix[0][1] * bandRotationMatrix[0][2]);
    quadraticRotations[4][4] = kSqrt01_04 * ((bandRotationMatrix[2][2] * bandRotationMatrix[2][2] - bandRotationMatrix[2][0] * bandRotationMatrix[2][0]) - (bandRotationMatrix[0][2] * bandRotationMatrix[0][2] - bandRotationMatrix[0][0] * bandRotationMatrix[0][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumOf5(inputIndex, inputCoefficients, quadraticRotations[4]);

    if (maxBand < 4)
        return;

    // band 3 - (Cubic) - Represents higher-order variations in lighting
    inputIndex += 5;
    float cubicRotations[7][7];

    cubicRotations[0][0] = kSqrt01_04 * ((bandRotationMatrix[2][2] * quadraticRotations[0][0] + bandRotationMatrix[2][0] * quadraticRotations[0][4]) + (bandRotationMatrix[0][2] * quadraticRotations[4][0] + bandRotationMatrix[0][0] * quadraticRotations[4][4]));
    cubicRotations[0][1] = kSqrt03_02 * (bandRotationMatrix[2][1] * quadraticRotations[0][0] + bandRotationMatrix[0][1] * quadraticRotations[4][0]);
    cubicRotations[0][2] = kSqrt15_16 * (bandRotationMatrix[2][1] * quadraticRotations[0][1] + bandRotationMatrix[0][1] * quadraticRotations[4][1]);
    cubicRotations[0][3] = kSqrt05_06 * (bandRotationMatrix[2][1] * quadraticRotations[0][2] + bandRotationMatrix[0][1] * quadraticRotations[4][2]);
    cubicRotations[0][4] = kSqrt15_16 * (bandRotationMatrix[2][1] * quadraticRotations[0][3] + bandRotationMatrix[0][1] * quadraticRotations[4][3]);
    cubicRotations[0][5] = kSqrt03_02 * (bandRotationMatrix[2][1] * quadraticRotations[0][4] + bandRotationMatrix[0][1] * quadraticRotations[4][4]);
    cubicRotations[0][6] = kSqrt01_04 * ((bandRotationMatrix[2][2] * quadraticRotations[0][4] - bandRotationMatrix[2][0] * quadraticRotations[0][0]) + (bandRotationMatrix[0][2] * quadraticRotations[4][4] - bandRotationMatrix[0][0] * quadraticRotations[4][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumof7(inputIndex, inputCoefficients, cubicRotations[0]);

    cubicRotations[1][0] = kSqrt01_06 * (bandRotationMatrix[1][2] * quadraticRotations[0][0] + bandRotationMatrix[1][0] * quadraticRotations[0][4]) + kSqrt01_06 * ((bandRotationMatrix[2][2] * quadraticRotations[1][0] + bandRotationMatrix[2][0] * quadraticRotations[1][4]) + (bandRotationMatrix[0][2] * quadraticRotations[3][0] + bandRotationMatrix[0][0] * quadraticRotations[3][4]));
    cubicRotations[1][1] = bandRotationMatrix[1][1] * quadraticRotations[0][0] + (bandRotationMatrix[2][1] * quadraticRotations[1][0] + bandRotationMatrix[0][1] * quadraticRotations[3][0]);
    cubicRotations[1][2] = kSqrt05_08 * bandRotationMatrix[1][1] * quadraticRotations[0][1] + kSqrt05_08 * (bandRotationMatrix[2][1] * quadraticRotations[1][1] + bandRotationMatrix[0][1] * quadraticRotations[3][1]);
    cubicRotations[1][3] = kSqrt05_09 * bandRotationMatrix[1][1] * quadraticRotations[0][2] + kSqrt05_09 * (bandRotationMatrix[2][1] * quadraticRotations[1][2] + bandRotationMatrix[0][1] * quadraticRotations[3][2]);
    cubicRotations[1][4] = kSqrt05_08 * bandRotationMatrix[1][1] * quadraticRotations[0][3] + kSqrt05_08 * (bandRotationMatrix[2][1] * quadraticRotations[1][3] + bandRotationMatrix[0][1] * quadraticRotations[3][3]);
    cubicRotations[1][5] = bandRotationMatrix[1][1] * quadraticRotations[0][4] + (bandRotationMatrix[2][1] * quadraticRotations[1][4] + bandRotationMatrix[0][1] * quadraticRotations[3][4]);
    cubicRotations[1][6] = kSqrt01_06 * (bandRotationMatrix[1][2] * quadraticRotations[0][4] - bandRotationMatrix[1][0] * quadraticRotations[0][0]) + kSqrt01_06 * ((bandRotationMatrix[2][2] * quadraticRotations[1][4] - bandRotationMatrix[2][0] * quadraticRotations[1][0]) + (bandRotationMatrix[0][2] * quadraticRotations[3][4] - bandRotationMatrix[0][0] * quadraticRotations[3][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumof7(inputIndex, inputCoefficients, cubicRotations[1]);

    cubicRotations[2][0] = kSqrt04_15 * (bandRotationMatrix[1][2] * quadraticRotations[1][0] + bandRotationMatrix[1][0] * quadraticRotations[1][4]) + kSqrt01_05 * (bandRotationMatrix[0][2] * quadraticRotations[2][0] + bandRotationMatrix[0][0] * quadraticRotations[2][4]) + -sqrt(1.0 / 60.0) * ((bandRotationMatrix[2][2] * quadraticRotations[0][0] + bandRotationMatrix[2][0] * quadraticRotations[0][4]) - (bandRotationMatrix[0][2] * quadraticRotations[4][0] + bandRotationMatrix[0][0] * quadraticRotations[4][4]));
    cubicRotations[2][1] = kSqrt08_05 * bandRotationMatrix[1][1] * quadraticRotations[1][0] + kSqrt06_05 * bandRotationMatrix[0][1] * quadraticRotations[2][0] + -kSqrt01_10 * (bandRotationMatrix[2][1] * quadraticRotations[0][0] - bandRotationMatrix[0][1] * quadraticRotations[4][0]);
    cubicRotations[2][2] = bandRotationMatrix[1][1] * quadraticRotations[1][1] + kSqrt03_04 * bandRotationMatrix[0][1] * quadraticRotations[2][1] + -kSqrt01_16 * (bandRotationMatrix[2][1] * quadraticRotations[0][1] - bandRotationMatrix[0][1] * quadraticRotations[4][1]);
    cubicRotations[2][3] = kSqrt08_09 * bandRotationMatrix[1][1] * quadraticRotations[1][2] + kSqrt02_03 * bandRotationMatrix[0][1] * quadraticRotations[2][2] + -kSqrt01_18 * (bandRotationMatrix[2][1] * quadraticRotations[0][2] - bandRotationMatrix[0][1] * quadraticRotations[4][2]);
    cubicRotations[2][4] = bandRotationMatrix[1][1] * quadraticRotations[1][3] + kSqrt03_04 * bandRotationMatrix[0][1] * quadraticRotations[2][3] + -kSqrt01_16 * (bandRotationMatrix[2][1] * quadraticRotations[0][3] - bandRotationMatrix[0][1] * quadraticRotations[4][3]);
    cubicRotations[2][5] = kSqrt08_05 * bandRotationMatrix[1][1] * quadraticRotations[1][4] + kSqrt06_05 * bandRotationMatrix[0][1] * quadraticRotations[2][4] + -kSqrt01_10 * (bandRotationMatrix[2][1] * quadraticRotations[0][4] - bandRotationMatrix[0][1] * quadraticRotations[4][4]);
    cubicRotations[2][6] = kSqrt04_15 * (bandRotationMatrix[1][2] * quadraticRotations[1][4] - bandRotationMatrix[1][0] * quadraticRotations[1][0]) + kSqrt01_05 * (bandRotationMatrix[0][2] * quadraticRotations[2][4] - bandRotationMatrix[0][0] * quadraticRotations[2][0]) + -sqrt(1.0 / 60.0) * ((bandRotationMatrix[2][2] * quadraticRotations[0][4] - bandRotationMatrix[2][0] * quadraticRotations[0][0]) - (bandRotationMatrix[0][2] * quadraticRotations[4][4] - bandRotationMatrix[0][0] * quadraticRotations[4][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumof7(inputIndex, inputCoefficients, cubicRotations[2]);

    cubicRotations[3][0] = kSqrt03_10 * (bandRotationMatrix[1][2] * quadraticRotations[2][0] + bandRotationMatrix[1][0] * quadraticRotations[2][4]) + -kSqrt01_10 * ((bandRotationMatrix[2][2] * quadraticRotations[3][0] + bandRotationMatrix[2][0] * quadraticRotations[3][4]) + (bandRotationMatrix[0][2] * quadraticRotations[1][0] + bandRotationMatrix[0][0] * quadraticRotations[1][4]));
    cubicRotations[3][1] = kSqrt09_05 * bandRotationMatrix[1][1] * quadraticRotations[2][0] + -kSqrt03_05 * (bandRotationMatrix[2][1] * quadraticRotations[3][0] + bandRotationMatrix[0][1] * quadraticRotations[1][0]);
    cubicRotations[3][2] = kSqrt09_08 * bandRotationMatrix[1][1] * quadraticRotations[2][1] + -kSqrt03_08 * (bandRotationMatrix[2][1] * quadraticRotations[3][1] + bandRotationMatrix[0][1] * quadraticRotations[1][1]);
    cubicRotations[3][3] = bandRotationMatrix[1][1] * quadraticRotations[2][2] + -kSqrt01_03 * (bandRotationMatrix[2][1] * quadraticRotations[3][2] + bandRotationMatrix[0][1] * quadraticRotations[1][2]);
    cubicRotations[3][4] = kSqrt09_08 * bandRotationMatrix[1][1] * quadraticRotations[2][3] + -kSqrt03_08 * (bandRotationMatrix[2][1] * quadraticRotations[3][3] + bandRotationMatrix[0][1] * quadraticRotations[1][3]);
    cubicRotations[3][5] = kSqrt09_05 * bandRotationMatrix[1][1] * quadraticRotations[2][4] + -kSqrt03_05 * (bandRotationMatrix[2][1] * quadraticRotations[3][4] + bandRotationMatrix[0][1] * quadraticRotations[1][4]);
    cubicRotations[3][6] = kSqrt03_10 * (bandRotationMatrix[1][2] * quadraticRotations[2][4] - bandRotationMatrix[1][0] * quadraticRotations[2][0]) + -kSqrt01_10 * ((bandRotationMatrix[2][2] * quadraticRotations[3][4] - bandRotationMatrix[2][0] * quadraticRotations[3][0]) + (bandRotationMatrix[0][2] * quadraticRotations[1][4] - bandRotationMatrix[0][0] * quadraticRotations[1][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumof7(inputIndex, inputCoefficients, cubicRotations[3]);

    cubicRotations[4][0] = kSqrt04_15 * (bandRotationMatrix[1][2] * quadraticRotations[3][0] + bandRotationMatrix[1][0] * quadraticRotations[3][4]) + kSqrt01_05 * (bandRotationMatrix[2][2] * quadraticRotations[2][0] + bandRotationMatrix[2][0] * quadraticRotations[2][4]) + -sqrt(1.0 / 60.0) * ((bandRotationMatrix[2][2] * quadraticRotations[4][0] + bandRotationMatrix[2][0] * quadraticRotations[4][4]) + (bandRotationMatrix[0][2] * quadraticRotations[0][0] + bandRotationMatrix[0][0] * quadraticRotations[0][4]));
    cubicRotations[4][1] = kSqrt08_05 * bandRotationMatrix[1][1] * quadraticRotations[3][0] + kSqrt06_05 * bandRotationMatrix[2][1] * quadraticRotations[2][0] + -kSqrt01_10 * (bandRotationMatrix[2][1] * quadraticRotations[4][0] + bandRotationMatrix[0][1] * quadraticRotations[0][0]);
    cubicRotations[4][2] = bandRotationMatrix[1][1] * quadraticRotations[3][1] + kSqrt03_04 * bandRotationMatrix[2][1] * quadraticRotations[2][1] + -kSqrt01_16 * (bandRotationMatrix[2][1] * quadraticRotations[4][1] + bandRotationMatrix[0][1] * quadraticRotations[0][1]);
    cubicRotations[4][3] = kSqrt08_09 * bandRotationMatrix[1][1] * quadraticRotations[3][2] + kSqrt02_03 * bandRotationMatrix[2][1] * quadraticRotations[2][2] + -kSqrt01_18 * (bandRotationMatrix[2][1] * quadraticRotations[4][2] + bandRotationMatrix[0][1] * quadraticRotations[0][2]);
    cubicRotations[4][4] = bandRotationMatrix[1][1] * quadraticRotations[3][3] + kSqrt03_04 * bandRotationMatrix[2][1] * quadraticRotations[2][3] + -kSqrt01_16 * (bandRotationMatrix[2][1] * quadraticRotations[4][3] + bandRotationMatrix[0][1] * quadraticRotations[0][3]);
    cubicRotations[4][5] = kSqrt08_05 * bandRotationMatrix[1][1] * quadraticRotations[3][4] + kSqrt06_05 * bandRotationMatrix[2][1] * quadraticRotations[2][4] + -kSqrt01_10 * (bandRotationMatrix[2][1] * quadraticRotations[4][4] + bandRotationMatrix[0][1] * quadraticRotations[0][4]);
    cubicRotations[4][6] = kSqrt04_15 * (bandRotationMatrix[1][2] * quadraticRotations[3][4] - bandRotationMatrix[1][0] * quadraticRotations[3][0]) + kSqrt01_05 * (bandRotationMatrix[2][2] * quadraticRotations[2][4] - bandRotationMatrix[2][0] * quadraticRotations[2][0]) + -sqrt(1.0 / 60.0) * ((bandRotationMatrix[2][2] * quadraticRotations[4][4] - bandRotationMatrix[2][0] * quadraticRotations[4][0]) + (bandRotationMatrix[0][2] * quadraticRotations[0][4] - bandRotationMatrix[0][0] * quadraticRotations[0][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumof7(inputIndex, inputCoefficients, cubicRotations[4]);

    cubicRotations[5][0] = kSqrt01_06 * (bandRotationMatrix[1][2] * quadraticRotations[4][0] + bandRotationMatrix[1][0] * quadraticRotations[4][4]) + kSqrt01_06 * ((bandRotationMatrix[2][2] * quadraticRotations[3][0] + bandRotationMatrix[2][0] * quadraticRotations[3][4]) - (bandRotationMatrix[0][2] * quadraticRotations[1][0] + bandRotationMatrix[0][0] * quadraticRotations[1][4]));
    cubicRotations[5][1] = bandRotationMatrix[1][1] * quadraticRotations[4][0] + (bandRotationMatrix[2][1] * quadraticRotations[3][0] - bandRotationMatrix[0][1] * quadraticRotations[1][0]);
    cubicRotations[5][2] = kSqrt05_08 * bandRotationMatrix[1][1] * quadraticRotations[4][1] + kSqrt05_08 * (bandRotationMatrix[2][1] * quadraticRotations[3][1] - bandRotationMatrix[0][1] * quadraticRotations[1][1]);
    cubicRotations[5][3] = kSqrt05_09 * bandRotationMatrix[1][1] * quadraticRotations[4][2] + kSqrt05_09 * (bandRotationMatrix[2][1] * quadraticRotations[3][2] - bandRotationMatrix[0][1] * quadraticRotations[1][2]);
    cubicRotations[5][4] = kSqrt05_08 * bandRotationMatrix[1][1] * quadraticRotations[4][3] + kSqrt05_08 * (bandRotationMatrix[2][1] * quadraticRotations[3][3] - bandRotationMatrix[0][1] * quadraticRotations[1][3]);
    cubicRotations[5][5] = bandRotationMatrix[1][1] * quadraticRotations[4][4] + (bandRotationMatrix[2][1] * quadraticRotations[3][4] - bandRotationMatrix[0][1] * quadraticRotations[1][4]);
    cubicRotations[5][6] = kSqrt01_06 * (bandRotationMatrix[1][2] * quadraticRotations[4][4] - bandRotationMatrix[1][0] * quadraticRotations[4][0]) + kSqrt01_06 * ((bandRotationMatrix[2][2] * quadraticRotations[3][4] - bandRotationMatrix[2][0] * quadraticRotations[3][0]) - (bandRotationMatrix[0][2] * quadraticRotations[1][4] - bandRotationMatrix[0][0] * quadraticRotations[1][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumof7(inputIndex, inputCoefficients, cubicRotations[5]);

    cubicRotations[6][0] = kSqrt01_04 * ((bandRotationMatrix[2][2] * quadraticRotations[4][0] + bandRotationMatrix[2][0] * quadraticRotations[4][4]) - (bandRotationMatrix[0][2] * quadraticRotations[0][0] + bandRotationMatrix[0][0] * quadraticRotations[0][4]));
    cubicRotations[6][1] = kSqrt03_02 * (bandRotationMatrix[2][1] * quadraticRotations[4][0] - bandRotationMatrix[0][1] * quadraticRotations[0][0]);
    cubicRotations[6][2] = kSqrt15_16 * (bandRotationMatrix[2][1] * quadraticRotations[4][1] - bandRotationMatrix[0][1] * quadraticRotations[0][1]);
    cubicRotations[6][3] = kSqrt05_06 * (bandRotationMatrix[2][1] * quadraticRotations[4][2] - bandRotationMatrix[0][1] * quadraticRotations[0][2]);
    cubicRotations[6][4] = kSqrt15_16 * (bandRotationMatrix[2][1] * quadraticRotations[4][3] - bandRotationMatrix[0][1] * quadraticRotations[0][3]);
    cubicRotations[6][5] = kSqrt03_02 * (bandRotationMatrix[2][1] * quadraticRotations[4][4] - bandRotationMatrix[0][1] * quadraticRotations[0][4]);
    cubicRotations[6][6] = kSqrt01_04 * ((bandRotationMatrix[2][2] * quadraticRotations[4][4] - bandRotationMatrix[2][0] * quadraticRotations[4][0]) - (bandRotationMatrix[0][2] * quadraticRotations[0][4] - bandRotationMatrix[0][0] * quadraticRotations[0][0]));

    outputCoefficients[outputIndex++] = ComputedWeightedSumof7(inputIndex, inputCoefficients, cubicRotations[6]);
}

#endif // SPHERICAL_HARMONICS_HLSL