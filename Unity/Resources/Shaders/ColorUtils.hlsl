// Copyright (c) 2024 Miris. All rights reserved.

// defines for indirect draw call elements
#pragma once

// Taken from https://chilliant.com/rgb2hsv.html
float3 HueToRgb(in float hue)
{
    float r = abs(hue * 6 - 3) - 1;
    float g = 2 - abs(hue * 6 - 2);
    float b = 2 - abs(hue * 6 - 4);
    return saturate(float3(r, g, b));
}