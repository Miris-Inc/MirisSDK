// Copyright (c) 2024 Miris. All rights reserved.

#pragma once

float3x3 rotateAboutYAxis(float angle) {
    float rad = radians(angle);
                
    float3x3 rotationMatrix = float3x3(
        cos(rad), 0, sin(rad),
        0,1,0,
        -sin(rad),0, cos(rad)
        );

    return rotationMatrix;
}

float3x3 quat2matrix(float4 quaternion) 
{
    // The quaternion should be normalized in the loading phase.
    // but we will re-normalize it here just in case
    float4 quatN = normalize(quaternion);
    // Extract real and imaginary parts
    float realPart = quatN.x;
    float imagX = quatN.y;
    float imagY = quatN.z;
    float imagZ = quatN.w;
    // Compute the rotation matrix from the quaternion
    float3x3 rotationMatrix = float3x3(
        1.0 - 2.0 * (imagX * imagX + imagY * imagY), 2 * (realPart * imagX - imagZ * imagY),
        2.0 * (realPart * imagY + imagZ * imagX), 2 * (realPart * imagX + imagZ * imagY),
        1.0 - 2.0 * (realPart * realPart + imagY * imagY), 2 * (imagX * imagY - imagZ * realPart),
        2.0 * (realPart * imagY - imagZ * imagX), 2 * (imagX * imagY + imagZ * realPart),
        1.0 - 2.0 * (realPart * realPart + imagX * imagX));
    return rotationMatrix;
}
