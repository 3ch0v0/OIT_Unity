#ifndef OIT_PPLL_INCLUDED
#define OIT_PPLL_INCLUDED

struct PPLLLinkedListNode
{
    uint pixelColor;
    uint uDepthSampleIdx;
    uint next;
};

// 将 float4 颜色打包成 uint 
inline uint PackRGBA(float4 unpackedInput)
{
    uint4 u = (uint4)(saturate(unpackedInput) * 255 + 0.5);
    return (u.w << 24UL) | (u.z << 16UL) | (u.y << 8UL) | u.x;
}

// 将 uint 解包为 float4
inline float4 UnpackRGBA(uint packedInput)
{
    uint4 p = uint4((packedInput & 0xFFUL),
                    (packedInput >> 8UL) & 0xFFUL,
                    (packedInput >> 16UL) & 0xFFUL,
                    (packedInput >> 24UL));
    return ((float4)p) / 255.0;
}

inline uint PackDepthSampleIdx(float depth, uint uSampleIdx) 
{
    uint d = (uint)(saturate(depth) * (pow(2, 24) - 1));
    return (d << 8UL) | (uSampleIdx & 0xFFUL);
}

inline float UnpackDepth(uint uDepthSampleIdx) 
{
    return (float)(uDepthSampleIdx >> 8UL) / (pow(2, 24) - 1);
}

// 提取 Sample Index
inline uint UnpackSampleIdx(uint uDepthSampleIdx) 
{
    return uDepthSampleIdx & 0xFFUL;
}

// 获取线性 01 深度
inline float ABufferLinear01Depth(float z, float4 zBufferParams)
{
    return 1.0 / (zBufferParams.x * z + zBufferParams.y);
}
#endif // OIT_LINKED_LIST_INCLUDED