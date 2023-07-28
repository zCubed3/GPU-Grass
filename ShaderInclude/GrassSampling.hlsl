/*
 * MIT License
 * 
 * Copyright (c) 2023 zCubed3
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
*/

#ifndef GRASS_SAMPLING_HLSL
#define GRASS_SAMPLING_HLSL

Texture2D<float4> _SurfaceMaterialLUT;
SamplerState sampler_SurfaceMaterialLUT;

float4 _GrassState;

float4 read_lut_blend_1d(float index, uint y) {
    uint low = clamp(floor(index), 0, _GrassState.y);
    uint high = clamp(ceil(index), 0, _GrassState.y);

    float blend = frac(index);

    return lerp(_SurfaceMaterialLUT[uint2(low, y)], _SurfaceMaterialLUT[uint2(high, y)], blend); 
}

float4 read_lut_blend_2d(float2 uv) {
    uint low = clamp(floor(uv.y), 0, _GrassState.z) + 2;
    uint high = clamp(ceil(uv.y), 0, _GrassState.z) + 2;

    float blend = frac(uv.y);

    float4 a = read_lut_blend_1d(uv.x, low);
    float4 b = read_lut_blend_1d(uv.x, high);

    return lerp(a, b, blend); 
}

#endif