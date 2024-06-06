#ifndef RECANOMAHO_VOLUMETRIC_LIGHT_UTILS_INCLUDED
#define RECANOMAHO_VOLUMETRIC_LIGHT_UTILS_INCLUDED

//光线经过介质时会产生距离上的衰减
float BeerLambert(float extinction, float depth)
{
    return exp(-extinction * depth);
}

//Horizon中为实现暗边效应使用的Beer-Powder函数
float BeerPowder(float extinction, float depth)
{
    return exp(-extinction * depth) * (1 - exp(-extinction * depth * 2)) * 2;
}

#endif