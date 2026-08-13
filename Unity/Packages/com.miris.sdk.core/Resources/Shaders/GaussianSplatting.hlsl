// Copyright © 2026 Miris, Inc. All rights reserved.

#ifndef GAUSSIAN_SPLATTING_HLSL
#define GAUSSIAN_SPLATTING_HLSL

// NOTE: this is mainly from https://github.com/aras-p/UnityGaussianSplatting as of Aug 27
// I copied the code solely to visualizing the gaussian splats from their GaussianSplatting.hlsl file,
// and created a simplified CalculateSplatView.

// NOTE: I have modified the functions to closely resemble the calculations found in this paper
// "EWA Splatting" by Zwicker. Please take a look at the paper, https://www.cs.umd.edu/~zwicker/publications/EWASplatting-TVCG02.pdf
// The equations of main interests are equation 31 and 34

#include "UnityCG.cginc"
#include "CommonConstants.hlsl"
#include "SphericalHarmonics.hlsl"
#include "MathUtils.hlsl"

struct SplatViewData
{
    float4 pos;
    float4 color;
    float2 majorAxis;
    float2 minorAxis;
};

// This function computes the 3D Gaussian Distribution for a
// splat in world space using scale and quaternion
// (note that the quaternion coming in ideally has been normalized)
float3x3 calculate3DGSCovariance(float4 rot, float3 scale) {
    float3x3 scaleMatrix = float3x3(scale.x, 0, 0, 0, scale.y, 0, 0, 0, scale.z);
    float3x3 rotationMatrix = quat2matrix(rot);

    // compute the transformation matrix M
    float3x3 M = mul(rotationMatrix, scaleMatrix);
   
    // The final covariance matrix
    // Sigma is obtained by multiplying the transpose of
    // M with M. This step ensures that the resulting covariance matrix is symmetric and positive
    // semi-definite, which are necessary propertiess for a valid covariance matrix
    float3x3 sigma = mul(M, transpose(M));

    return sigma;
}

// Project the 3D gaussian into a 2d plane
// from "EWA Splatting" (Zwicker et al 2002) eq. 31 <--Make sure to understand this equation
float3 map3DGSCovarianceTo2DPlane(float3 splat3DCenter, float3x3 cov3d, float4x4 matrixV,
                                  float4x4 matrixP, float4 screenParams) {

    // Transform the splat center to view space
    float4x4 viewMatrix = matrixV;
    float3 splatCenterInViewSpace = mul(viewMatrix, float4(splat3DCenter, 1)).xyz;

    // This is necessary to avoid extreme projection distortions
    float aspect = matrixP._m00 / matrixP._m11;
    float tanFovX = rcp(matrixP._m00);
    float tanFovY = rcp(matrixP._m11 * aspect);
    float limX = 1.3 * tanFovX;
    float limY = 1.3 * tanFovY;

    // Do perspective divide
    float xOverZ = splatCenterInViewSpace.x / splatCenterInViewSpace.z;
    float yOverZ = splatCenterInViewSpace.y / splatCenterInViewSpace.z;

    // clamp the splat center to stay within FOV limits
    splatCenterInViewSpace.x = clamp(xOverZ, -limX, limX) * splatCenterInViewSpace.z;
    splatCenterInViewSpace.y = clamp(yOverZ, -limY, limY) * splatCenterInViewSpace.z;

    float focal = screenParams.x * matrixP._m00 / 2;

    // for clarity, I'm going to copy splatCenterInViewSpace into an array.
    // This will match the Jacobian equations found in the EWA Splatting paper -- see eq 34
    float t[3] = {splatCenterInViewSpace.x, splatCenterInViewSpace.y, splatCenterInViewSpace.z};

    float3x3 Jacobian = float3x3(focal / t[2], 0, -(focal * t[0]) / (t[2] * t[2]), 0, focal / t[2],
                                 -(focal * t[1]) / (t[2] * t[2]), 0, 0, 0);

    // Here we start computing the final mapping from 3d to 2d. Essentially, we are computing equation 31
    // in the EWA Splatting paper. Vk is our final projection
    float3x3 W = (float3x3)viewMatrix;
    float3x3 T = mul(Jacobian, W);

    float3x3 V = float3x3(cov3d._m00, cov3d._m01, cov3d._m02, cov3d._m01, cov3d._m11, cov3d._m12,
                          cov3d._m02, cov3d._m12, cov3d._m22);

    float3x3 Vk = mul(T, mul(V, transpose(T)));

    // Low pass filter to make each splat at least 1px size.
    Vk._m00 += 0.3;
    Vk._m11 += 0.3;

    // THIS IS VERY IMPORTANT TO UNDERSTAND. This is the data that will modify our quad geometry
    // Vk[0][0]: variance in x-direction
    // Vk[0][1]: covariance between the x and y directions
    // Vk[1][1]: variance in y direction
    //
    // why these values?
    // The covariance matrix in 2D can be represented as an ellipse, where the variances along the x and y directions
    // determine the lengths of the major and minor axes, and the covariance determines the orientation of the ellipse.
    // by returning the variances cov[0][0] and cov[1][1] and the covariance cov[0][1], you have
    // enough info to describe the ellipse's shape and orientation

    return float3(Vk._m00, Vk._m01, Vk._m11);
}

float getDeterminant(float3 cov2d) {
    return cov2d.x * cov2d.z - cov2d.y * cov2d.y;
}

// Extracts the elements of the 2D covariance and return them as conic coefficients (x,y,z) which describes
// the shape and orientation of the gaussian splat in screen space
float3 computeConicCoefficient(float3 cov2d) {

    float determinant = getDeterminant(cov2d);

    if (determinant == 0) {
        return float3(0, 0, 0);
    }

    float determinantInverse = 1.0f / determinant;

    float3 conicCoefficient = float3(cov2d.z * determinantInverse, 
                                    -cov2d.y * determinantInverse,
                                     cov2d.x * determinantInverse);

    return conicCoefficient;
}

// Computes the principal axis vectors that define the size and orientation of the
// ellipse representing the gaussian splat
void computeGaussianAxisExtent(float3 cov2d, out float2 majorAxis, out float2 minorAxis) {

    // Keep this in mind:
    //
    // cov2d.x=variance along the x-axis
    // cov2d.z=variance along the y-axis
    // cov2d.y=covariance between the x and y directions
    //
    // The variances along the x and y directions determine the lengths of the major and minor axes,
    // and the covariance determines the orientation of the ellipse

    float averageCovariance = 0.5 * (cov2d.x + cov2d.z);

    //Compute the eigen values of the 2D covariance matrix, which represents the spread of the gaussian
    float radius = length(float2((cov2d.x - cov2d.z) / 2.0, cov2d.y));
    float lambda1 = averageCovariance + radius;
    float lambda2 = max(averageCovariance - radius, 0.1);

    // compute the Ellipse axis vectors

    // compute the direction vector for the ellipse's major axis
    float2 diagVec = normalize(float2(cov2d.y, lambda1 - cov2d.x));

    // flip the y component for correct orientation (may not be needed)
    diagVec.y = -diagVec.y;

    // define a maximum size for the axis to avoid too large splat
    float maxScreenResolution = 2048.0;

    // Compute the two principal axis of the ellipse (major axis, minor axis)
    majorAxis = min(sqrt(2.0 * lambda1), maxScreenResolution) * diagVec; //major axis
    minorAxis =
        min(sqrt(2.0 * lambda2), maxScreenResolution) * float2(diagVec.y, -diagVec.x); //minor axis
}

// Calculate the intensity of the Gaussian splat at a particular screen space location. It determines
// how much influence a gaussian splat has at a given pixel based on its spread and location.
float calculateGaussianPowerFromConic(float3 conicCoefficient,
                                      float2 screenSpaceDelta) {
    // apply the conic equation to calculate the gaussian's influence at this pos.
    return -0.5 * (conicCoefficient.x * screenSpaceDelta.x * screenSpaceDelta.x +
                   conicCoefficient.z * screenSpaceDelta.y * screenSpaceDelta.y) +
           conicCoefficient.y * screenSpaceDelta.x * screenSpaceDelta.y;
}

// compute the screen space delta between the quad's vertex position and the gaussian center in screen space
float2 computeScreenSpaceDelta(float2 vertexScreenPosition,
                               float2 gaussianCenterScreenPosition,
                               float projectionSignY) {

    // compute the difference bewteen the vertex position and the gaussian center
    float2 screenSpaceDelta = vertexScreenPosition - gaussianCenterScreenPosition;

    // invert y coordinate
    screenSpaceDelta.y *= projectionSignY;
    return screenSpaceDelta;
}

// Calculation to get the current quad vertex
// What we are doing is calculating the position of each vertex of a quad. i.e.
// v0=float2(0,0)
// v1=float2(1,0)
// v2=float2(0,1)
// v3=float2(1,1)
//
// - Since we are rendering a quad, vertexID will range from 0-3.
//
// - The vertexId&1 expression extracts the least significat bit of vertexId. So, when
// vertexId=0 -> 0
// vertexId=1 -> 1 (remember 1 in binary is 01)
// vertexId=2 -> 0 (2 in birnary is 10)
// vertexId=3 -> 1 (3 is 11)
//
// - The (vertexId>>1)&1 expression does a right shift on vertexId by 1 bit and then gets the least significant bit
// so, when
// vertexId=0 -> 0
// vertexId=1 -> 0 (again 1 in birnary is 01)
// vertexId=2 -> 1
// vertexId=3 -> 1
//
// When you combine these expressions you get the quad coordinates :)
// v0=float2(0,0)
// v1=float2(1,0)
// v2=float2(0,1)
// v3=float2(1,1)

float2 getCurrentQuadVertex(uint vertexId) {
    return float2(vertexId & 1, (vertexId >> 1) & 1);
}

#endif // GAUSSIAN_SPLATTING_HLSL
