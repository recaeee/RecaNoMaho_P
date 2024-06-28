# 【RecaNoMaho】特化阴影方案PlanarShadowmap

#### 0 写在前面

RecaNoMaho是我的开源Unity URP项目，在其中我会致力于收录一些常见的、猎奇的、有趣的渲染效果与技术，并以源码实现与原理解析呈现，以让该系列内容具有一定的学习价值与参考意义。为了保证一定的可移植性、可迭代性和轻量性，在这些渲染效果与技术的实现中，第一原则是能写成RenderFeature的尽量写成RenderFeature，代价是原本一些修改URP代码很方便能做的事情可能要绕点路，以及丢失一些管线层面的可能优化点，这些优化可以在实际实践中再去实现。个人能力有限，文内如果有误或者有更好的想法，欢迎一起讨论~

RecaNoMaho项目仓库：https://github.com/recaeee/RecaNoMaho_P

目前使用的Unity版本：2022.3.17f1c1

目前使用的URP版本：14.0.9

本篇属于对阴影贴图的一种特化优化方案，其结合了平面阴影Planar Shadow与阴影贴图Shadowmap两种阴影技术，适用于平坦开阔地形下的场景，属于SLG类型游戏、MOBA类型游戏阴影方案的特化对策卡。该技术在尽量无损的情况下降低了CPU端渲染的负担、GPU端渲染的耗时以及带宽压力，该技术主要参考了苍白的茧的[【Unity SLG大地图的绝佳阴影方案-平面阴影】](https://zhuanlan.zhihu.com/p/688929547)，感谢大佬的无私分享，一方面我按自己的理解重新讲述下该方案的原理与优劣，另一方面是记录一些实践中遇到的实际问题与处理。

![20240628124625](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240628124625.png)

#### 1 平面阴影Planar Shadow

因为**Planar Shadowmap**是结合**平面阴影Planar Shadow**和**阴影贴图Shadowmap**的一项技术，那么首先来简单了解下什么是**平面阴影Planar Shadow**。

平面阴影Planar Shadow是一项很古早且成熟的阴影渲染技术，不像Shadowmap会使用一张高精度的RenderTexture来记录光源空间下所有物体的深度信息来渲染阴影，平面阴影本质上渲染阴影的一种**低成本Trick**（也就是一种骗术），**当物体的阴影只投射在平面上时，可以直接将物体（的所有三角形顶点）投影到该平面上，并渲染成阴影的颜色**，就可以得到视觉正确的阴影效果了。它的原理非常简单，**不需要任何额外的RenderTexture**就可以得到**高质量**的阴影，**性能也非常好**，但高性能、高质量的阴影的代价正如其名，**只适用于投射到平面的阴影**，并且拥有其他一系列弊端，比如**无法实现自阴影**。

该技术的全名叫做**平面投影阴影Planar Projected Shadows**，由Jim Blinn 1988年提出，历史非常悠久，工业界也有很多对该技术的极佳实践与改进，并且存在大量的工程应用实践，在《王者荣耀》中就使用了类似的阴影渲染技术。网上对该技术的分享可以参考[Unity平面阴影(王者荣耀阴影实现)](https://zhuanlan.zhihu.com/p/42781261)、[使用顶点投射的方法制作实时阴影](https://zhuanlan.zhihu.com/p/31504088)、[【Unity Shader】平面阴影（Planar Shadow）](https://zhuanlan.zhihu.com/p/266174847)。

![20240628143147](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240628143147.png)

[图片引用](https://www.google.com/url?sa=i&url=https%3A%2F%2Funwire.hk%2F2021%2F09%2F05%2Fchinese-teen-online-game-server-down%2Fgame-channel%2F&psig=AOvVaw2I4OV01KmBYzRfuilAsv3g&ust=1719642689232000&source=images&cd=vfe&opi=89978449&ved=0CBEQjRxqFwoTCLiVzLDW_YYDFQAAAAAdAAAAABAE)

接下来，就来讲讲平面阴影的简要实现步骤：

1. **顶点投影**：在顶点着色器中，我们需要确定Mesh在投影到平面后的形状，该过程非常简单，只需要在顶点着色器中求每个顶点在平面上的投影位置，即**点在光源方向上投影到平面上的坐标**。已知顶点的positionWS，方向光源方向dirLightDir，与x-z平面平行的平面的高度planarHeight，就可以非常容易得到顶点在光源方向上投影到平面的坐标。

    只考虑二维情况，顶点投影示意图如下，标准化后的光源方向为dirLightDir，自定义的平面高度为_PlanarHeight，要投影的顶点为Vertex，投影后的顶点为Projected Vertex。

    ![20240627195016](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240627195016.png)

2. **混合阴影**：在片元着色器中，简单地使用Alpha Blend，将阴影的颜色混合到地面上。

混合阴影时需要注意，重叠的阴影会导致阴影结果错误，可以使用Stencil来解决。

同时，也可以用顶点算阴影衰减来一定程度上模拟PCSS的效果，具体可以参考[【使用顶点投射的方法制作实时阴影】](https://zhuanlan.zhihu.com/p/31504088)。

**平面阴影Planar Shadow的优点**：

1. 只需要额外一次DrawCall，没有额外的RenderTexture（和贴图采样），内存、带宽占用少，渲染消耗最小。

**平面阴影Planar Shadow的缺点**：

1. 阴影只能投射到平面上。

2. 不支持自阴影。

3. 一个物体的阴影无法投影到另一个物体上

4. 阴影的效果需要根据AlphaBlend的结果手动调整，不能与PBR光照计算交互。

在项目中也提供了Planar Shadow的简单实现供参考。

#### 2 平面阴影贴图Planar Shadowmap

对于平面阴影Planar Shadow的这4个缺点，普通的**实时阴影贴图Shadowmap**都是可以解决的，阴影可以投射到任何物体上，也支持自阴影，可以在PBR光照计算中作为shadowAttenuation参与光照计算。而普通的实时阴影贴图Shadowmap具有以下几个缺点：

1. Shadowmap像素利用率通常来说不高，可能存在很多空像素或对实际画面无贡献的像素，当然确实有一系列优化阴影裁剪视锥体的方案。

2. 有一定渲染消耗，需要额外的Shadowmap RenderTexture，会占用一定量内存，Store时会造成带宽消耗，如果使用CSM方案，还会导致更多的DrawCall。

3. Shadowmap单个像素的信息量与实际渲染画面的单个像素的信息量不匹配，这也是CSM所解决的一个问题点，也就是让距离摄像机近的空间在阴影贴图上占有更多的像素，以此来提高近处的阴影精度。而不采用CSM时，则实际渲染画面上对于摄像机不同远近(position)的片元对应的阴影精度几乎都是一样的。

可以看到，虽然实时阴影贴图Shadowmap解决了平面阴影Planar Shadow的一些问题，但自己也存在一些缺点。由此，**平面阴影贴图Planar Shadowmap**便登场了。**Planar Shadowmap将顶点投影后的信息记录在Shadowmap上，通过该信息，在光照着色时可以还原出片元所处位置的阴影信息**。Planar Shadowmap RenderTexture，格式和普通的Shadowmap相同，分辨率同ColorAttachment（也可以按比例缩放，也可以自定义）。

接下来详细阐述平面阴影贴图Planar Shadowmap的原理：

##### 2.1 Planar ShadowCaster Pass

采用**顶点投影**的方法，渲染所有ShadowCaster，**将XZ平面上每个点在光源方向上接受到的遮挡物的最大高度maxShadowHeight**信息记录到Planar Shadowmap。

只考虑XY二维的情况下，存储maxShadowHeight的过程如下图所示。首先将Origin Mesh投影到X平面上，顶点着色器的X坐标输出如图中Projected Mesh所示，pv0点对应原来的v0，所以pv0点对应的maxShadowHeight是y0，同理pv1点对应的maxShadowHeight是y1。图中绿色部分即Planar Shadowmap中每个像素的实际存储值，可以看到，在pv0对应的像素上，存储的值为y0。

![20240628114541](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240628114541.png)

其实这里稍微有点容易疑惑，为什么要存这个信息呢？我们可以这么理解：对于普通的阴影贴图，我们**直接存储了光源空间下光线能经过的最大距离maxDistance**（当光线遇到遮挡物了，认为光线到达了终点）。而Planar Shadowmap其实是**换一个形式**存储了这一最大距离，因为当我们知道XZ平面一个点，以及该点在光源方上接受到的遮挡物的最大高度maxShadowHeight，我们可以**唯一确定maxDistance**，只需要做一次简单的相似三角形计算。回看上图，对于普通的阴影贴图，我们直接存储v0点在光源空间的深度，这样，我们就直接知道了v0到pv0之间处于阴影中；而对于Planar Shadowmap，则稍微饶了点路，我们**在已知pv0、dirLightDir和pv0点存储的像素值y0，我们也可以求得v0点的信息，可以知道v0点存在物体，从而知道v0到pv0之间处于阴影中**。

接下来，看看渲染ShadowCaster的具体过程。

在顶点着色器中，和平面阴影一样的做法，**将顶点投影到平面上**得到planarShadowmapPositionWS，再将该坐标变换到HClipSpace得到positionCS以渲染到Planar Shadowmap上，可以将这一坐标变换过程理解为**XZ平面的二维坐标到PlanarShadowmap的UV的一种映射关系**。接下来，**考虑如何在像素中写入maxShadowHeight**，将顶点原本的高度Remap到[0,1]，因为Planar Shadowmap上像素值的范围是[0,1]，在这里我选择使用最简单的线性映射，在其中，我们需要确定全局的最大有效阴影高度_GlobalMaxShadowHeight，对应RemapWorldSpaceHeightToShadowHeight函数。由于和Shadowmap写入的是深度信息，我们需要**在positionCS的z分量中使用该值**，以存入深度图，而在片元着色器，则可以直接简单返回1。

以下为ShadowCaster的主要代码。

```c++
PlanarShadowmapShadowCasterVaryings planarShadowmapShadowCasterVert(PlanarShadowmapShadowCasterAttributes input)
{
    PlanarShadowmapShadowCasterVaryings output = (PlanarShadowmapShadowCasterVaryings)0;
            
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionOS = input.positionOS.xyz;
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 dirLightDir = normalize(GetMainLight().direction);
#if defined(NORMAL_BIAS)
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float invNdotL = 1.0 - saturate(dot(dirLightDir, normalWS));
    float scale = invNdotL * _PlanarShadowmapShadowBias.y;
    positionWS -= lerp(normalWS * scale.xxx, 0, RemapWorldSpaceHeightToShadowHeight(positionWS.y));
#endif

    //对于WorldSpace，存储摄像机视野内XZ平面上在阴影中的最大高度maxShadowHeight，坐标映射到HClipSpace。
    //求世界空间下投影坐标planarShadowmapPositionWS
    float3 planarShadowmapPositionWS = float3(0, 0, 0);
    planarShadowmapPositionWS.xz = positionWS.xz - dirLightDir.xz * max(0, positionWS.y - _PlanarHeight) / dirLightDir.y;
    planarShadowmapPositionWS.y = _PlanarHeight;
            
    //在阴影中的最大高度maxShadowHeight，归一化到[0,1]
    float maxShadowHeight = RemapWorldSpaceHeightToShadowHeight(positionWS.y);
#if defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
    maxShadowHeight = 1 - maxShadowHeight;
#endif

    //坐标映射到HClipSpace
    output.positionCS = TransformWorldToHClip(planarShadowmapPositionWS);
    //不能使用以下写法，手动/w会产生问题
    // output.positionCS = float4(output.positionCS.xy / output.positionCS.w, maxShadowHeight, 1);
    //正确写法如下
    output.positionCS.z = maxShadowHeight * output.positionCS.w;
            
    return output;
}

float RemapWorldSpaceHeightToShadowHeight(float heightWS)
{
    return (heightWS - _PlanarHeight) / (_GlobalMaxShadowHeight - _PlanarHeight);
}

float4 planarShadowmapShadowCasterFrag(PlanarShadowmapShadowCasterVaryings input) : SV_Target
{
    return 1;
}
```

##### 2.2 Lit Pass

在对物体进行PBR着色过程中，**将片元在光源方向上投影到XZ平面上**，查询Planar Shadowmap上该投影点接受到的遮挡物的最大高度maxShadowHeight，如果实际片元高度大于maxShadowHeight，则不在阴影中；否则，在阴影中。

可以这么理解，考虑两种情况：

1. 片元投影后，采样到的Planar Shadowmap上的像素值为0，这个时候显然知道，对于**以光源方向平行且经过该片元的位置的直线上**，光源可以直接照射到_PlanarHeight的平面上，我们假定片元必然处于平面以上，那么也就意味着片元不在阴影中。如下图所示的Fragment情况。对于Fragment，投影到X平面后，采样得到的Value为0。

![20240628123549](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240628123549.png)

2. 片元投影后，采样到的Planar Shadowmap上的像素点值大于0，则说明光路上存在遮挡物，这时候就需要判断片元和遮挡物哪个距离光源更近，因为片元和遮挡物处于与光源方向平行的直线上，那么直接比较两者高度就可以知道哪个距离光源更近了，从而得到片元位置的阴影信息。如下图所示，对于Fragment，投影到X平面，采样得到Value为y1，此时可以通过y1与dirLightDir还原出ShadowInfo点（蓝色点），可以知道蓝色点对Fragment造成了阴影遮挡，导致Fragment处于阴影中。

![20240628124035](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240628124035.png)

具体到着色时**计算shadowAttenuation**的过程也很简单。在顶点着色器中，将顶点的positionWS映射到Planar Shadowmap上，得到planarShadowCoord，再将顶点的positionWS.y以和之前相同的方法重映射到[0,1]得到shadowHeight。在片元着色器中，我们就可以通过shadowHeight和采样PlanarShadowmap的值做比较，得到shadowAttenuation。

```c++
PlanarShadowmapLitPassVaryings planarShadowmapLitPassVert(PlanarShadowmapLitPassAttributes input)
{
    PlanarShadowmapLitPassVaryings output = (PlanarShadowmapLitPassVaryings)0;
            
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionOS = input.positionOS.xyz;
    float3 positionWS = TransformObjectToWorld(positionOS);
    output.positionCS = TransformWorldToHClip(positionWS);
    output.planarShadowCoord = RemapWorldPositionToPlanarShadowmapCoord(positionWS);
    output.shadowHeight = saturate(RemapWorldSpaceHeightToShadowHeight(positionWS.y));
            
    return output;
}

float4 RemapWorldPositionToPlanarShadowmapCoord(float3 positionWS)
{
    float3 dirLightDir = normalize(GetMainLight().direction);
    float3 planarShadowmapPositionWS = float3(0, 0, 0);
    planarShadowmapPositionWS.xz = positionWS.xz - dirLightDir.xz * max(0, positionWS.y - _PlanarHeight) / dirLightDir.y;
    planarShadowmapPositionWS.y =  _PlanarHeight;

    float4 planarShadowmapPositionCS = mul(_PlanarShadowmapVP, float4(planarShadowmapPositionWS, 1.0));

    float4 planarShadowmapCoord = ComputeScreenPos(planarShadowmapPositionCS);
    return planarShadowmapCoord;
}
```

单纯计算shadowAttenuation的片元着色器代码如下。

```c++
float4 planarShadowmapLitPassFrag(PlanarShadowmapLitPassVaryings input) : SV_Target
{
    float2 shadowCoord = input.planarShadowCoord.xy / input.planarShadowCoord.w;
    return MainLightRealtimePlanarShadow(input.shadowHeight + _PlanarShadowmapShadowBias.x, shadowCoord);
}

float MainLightRealtimePlanarShadow(float shadowHeight, float2 planarShadowmapCoord)
{
#if defined(PLANAR_SHADOWMAP_LIT_PASS_PCF2X2)
    if(shadowHeight < 1)
    {
        half4 attenuation4 = 0;
        attenuation4.x = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(-0.5f, -0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));
        attenuation4.y = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(-0.5f, 0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));
        attenuation4.z = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(0.5f, 0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));
        attenuation4.w = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(0.5f, -0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));

        return dot(attenuation4, real(0.25));
    }
    else//高度大于等于有效阴影范围时回退到PointFilter，否则出错
    {
        float maxShadowHeight = SAMPLE_TEXTURE2D_X(_PlanarShadowmapTex, sampler_LinearClamp, planarShadowmapCoord);
#if defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
        maxShadowHeight = 1 - maxShadowHeight;
#endif
        return 1 - (shadowHeight <= maxShadowHeight);
    }
#elif defined(PLANAR_SHADOWMAP_LIT_PASS)
    return SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.x, planarShadowmapCoord.y, shadowHeight)).r;
    float maxShadowHeight = SAMPLE_TEXTURE2D_X(_PlanarShadowmapTex, sampler_LinearClamp, planarShadowmapCoord);
#if defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
    maxShadowHeight = 1 - maxShadowHeight;
#endif
    return 1 - (shadowHeight < maxShadowHeight);
#else
    return 1;
#endif
}
```

以上便是Planar Shadowmap的主要实现原理，思路是非常简洁的，过程中涉及到的计算也不复杂。从中，我们可以体会到，PlanarShadowmap利用了平面阴影中的顶点投影思路，以及Shadowmap的阴影信息存储方法。

Planar Shadowmap的阴影效果如下（光照简化为纯色，只输出shadowAttenuation）。

![20240628124625](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240628124625.png)

对应的Planar Shadowmap如下。

![20240628124708](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240628124708.png)

其实可以看到，在与光源方向接近相切的表面Artifacts是比较严重的，在一定距离后，该Artifacts的程度会减弱，整体效果需要看接受程度，MOBA或SLG类的摄像机距离下还行。

Planar Shadowmap具有以下**优点**:

1. **省去阴影视锥体裁剪**，以及大量ShadowCasterPass需要的CPU运算，**减少CPU耗时**。在真机（Qualcomm SDM845）上测试可以将阴影Pass的CPU耗时降低1~2ms左右。

2. 由于Planar Shadowmap存储的阴影信息位于主摄像机的HClip空间，所以**阴影贴图的利用率非常高**，可以在90%以上。1920x1080的Planar Shadowmap质量和2048x2048的传统Shadowmap相等，但尺寸减少了一半左右，**降低了RT所需的内存**，也**降低了带宽压力**。在真机（Qualcomm SDM845）上测试，带宽降低100M左右（因机型而异）。

3. **GPU耗时更少**，只需要在顶点着色器中做一次相似三角形的计算，不需要在片元着色器重做矩阵变换到光源空间。在真机（Qualcomm SDM845）上测试GPU耗时降低0.8ms。

当然说了这么多优点，Planar Shadowmap也存在很多**缺点**：

1. **与光源方向接近相切的表面阴影锯齿严重**。可以通过Depth Bias以及Normal Bias一定程度上处理。

2. **有效阴影的高度范围有限制**，需要映射到[0,1]的范围越大，阴影精度越低。在20m以内精度是比较高的。

3. 插入到地面以下的物体较难处理。

4. 当光源方向与摄像机方向在XZ平面夹角大于180°时，需要将阴影贴图的渲染范围扩大，以包含超出视野的投影信息。

#### 3 实践问题与处理

接下来，按问题的严重程度从大到小讲讲我在实践中遇到的问题。

##### 3.1 处理采样点越界

首先是对于第4个缺点的拓展，当光源方向与摄像机方向在XZ平面夹角大于180°时，需要将阴影贴图的渲染范围扩大，以包含超出视野的投影信息，简单点可以说是**采样点越界**问题。如下图所示，采样点越界，导致原本不该有阴影的地方产生了阴影。

![20240628142307](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240628142307.png)

首先来说说该问题的成因。考虑如下情况，当一个ShadowCaster经过顶点投影后的顶点着色器输出超出了Viewport，就会导致一部分信息超出Shadowmap，无法存入Shadowmap。如下图所示，灰色的maxShadowHeight值无法存入Planar Shadowmap，其中Shadowmap Boundary可以理解为Texture的边界，也可以理解为视野的边界，因为Planar Shadowmap存储屏幕空间的信息。

![20240628135327](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240628135327.png)

这就导致了一个情况，**视野内的一个片元可能投影到Shadowmap Boundary以外**，这个时候怎么采样都是不对的，用Clamp或Repeat都无法还原真实的阴影信息。而这种情况，在当光源方向与摄像机方向在XZ平面夹角大于180°时经常出现。如下图所示，Fragment投影到了Shadowmap以外，此时如果Clamp到边界，Fragment.y也会小于采样得到的maxShadowHeight，从而认为Fragment处于阴影中，但显然Fragment不在阴影中。

![20240628135805](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240628135805.png)

该问题的解决方法我思考了很久，但依然没找到一个取巧的方法去处理，最终还是选择了在ShadowCaster Pass中，**对MainCamera的位置做一个偏移，使其渲染范围扩大到足以包含越界的区域，从而使这些区域得到正确的阴影**。但这样，也引入了一个问题，阴影贴图的利用率不再接近100%，经过经验测试**利用率可能在60%到90%**，这是比较伤的一点，但防止渲染出错更重要。

实际上，对于给定的_PlanarHeight、_GlobalMaxShadowHeight、dirLightDir，越界的范围是确定的，因此对MainCamera需要调整的偏移量也是固定的。该偏移可以通过手动调试的经验给定，也可以基于数学计算，在项目中为简化问题，提供了一个手动的偏移值设置参数。工程实践中也可以考虑动态计算该偏移值以取得最好的效果。

##### 3.2 使用Bias处理Artifacts

由于不再是传统的Shadowmap方案，所以我们也不能直接使用传统的Depth Bias和Normal Bias了。因此我们选择在Shader中主动设置Bias。

对于Depth Bias，选择在Lit Pass中逐物体设置，以取得最好的效果。

```c++
float4 planarShadowmapLitPassFrag(PlanarShadowmapLitPassVaryings input) : SV_Target
{
    float2 shadowCoord = input.planarShadowCoord.xy / input.planarShadowCoord.w;
    return _BaseColor * MainLightRealtimePlanarShadow(input.shadowHeight + _PlanarShadowmapShadowBias.x, shadowCoord);
}

float MainLightRealtimePlanarShadow(float shadowHeight, float2 planarShadowmapCoord)
{
    ...
    float maxShadowHeight = SAMPLE_TEXTURE2D_X(_PlanarShadowmapTex, sampler_LinearClamp, planarShadowmapCoord);
#if defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
    maxShadowHeight = 1 - maxShadowHeight;
#endif
    return 1 - (shadowHeight < maxShadowHeight);
#else
    return 1;
#endif
}
```

对于Normal Bias，选择在渲染ShadowCaster时对Mesh进行收缩。

```c++
PlanarShadowmapShadowCasterVaryings planarShadowmapShadowCasterVert(PlanarShadowmapShadowCasterAttributes input)
{
    PlanarShadowmapShadowCasterVaryings output = (PlanarShadowmapShadowCasterVaryings)0;
            
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionOS = input.positionOS.xyz;
    float3 positionWS = TransformObjectToWorld(positionOS);
    float3 dirLightDir = normalize(GetMainLight().direction);
#if defined(NORMAL_BIAS)
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float invNdotL = 1.0 - saturate(dot(dirLightDir, normalWS));
    float scale = invNdotL * _PlanarShadowmapShadowBias.y;
    positionWS -= lerp(normalWS * scale.xxx, 0, RemapWorldSpaceHeightToShadowHeight(positionWS.y));
#endif

    ...
            
    return output;
}
```

#### 4 拓展优化

Planar Shadowmap本质上是修改了Shadowmap像素存储的信息，所以很多对Shadowmap的优化，Planar Shadowmap都可以使用，由此组成热血沸腾的武魂融合技。

##### 4.1 PCF

首当其冲，便是PCF的软阴影，这个也比较简单。

```c++
    if(shadowHeight < 1)
    {
        half4 attenuation4 = 0;
        attenuation4.x = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(-0.5f, -0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));
        attenuation4.y = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(-0.5f, 0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));
        attenuation4.z = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(0.5f, 0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));
        attenuation4.w = SAMPLE_TEXTURE2D_SHADOW(_PlanarShadowmapTex, sampler_PlanarShadowmapTex, float3(planarShadowmapCoord.xy + float2(0.5f, -0.5f) * _PlanarShadowmapTex_TexelSize.xy, shadowHeight));

        return dot(attenuation4, real(0.25));
    }
    else//高度大于等于有效阴影范围时回退到PointFilter，否则出错
    {
        float maxShadowHeight = SAMPLE_TEXTURE2D_X(_PlanarShadowmapTex, sampler_LinearClamp, planarShadowmapCoord);
#if defined(SHADER_API_GLES) || defined(SHADER_API_GLES3)
        maxShadowHeight = 1 - maxShadowHeight;
#endif
        return 1 - (shadowHeight <= maxShadowHeight);
    }
```

##### 4.2 Shadow Cache

Planar Shadowmap的Shadow Cache策略也非常简单，毕竟直接使用了Camera的CullingResult，使用context.CreateRendererList直接对CullingResult进行动静LayerMask的筛选，每帧只渲染动态的部分即可。在更实际的运用中，还需要考虑场景管理，这是Shadow Cache非常重要的部分，在项目中的Shadow Cache写的非常初步，仅供参考。实现方法可以参考[【Unity改造URP的CSM阴影】](https://zhuanlan.zhihu.com/p/691367954)，在Shadow Cache的基础上，可以再拓展Scrolling的技术进一步优化性能。

```c++
                if (globalParams.shadowCache && IsCachedPlanarShadowmapDirty(camera))
                {
                    //Render CachedPlanarShadowmapTex "Only when cache is dirty"
                    RendererListDesc cachedRendererListDesc =
                        new RendererListDesc(shaderTagIds.ToArray(), renderingData.cullResults, camera);
                    cachedRendererListDesc.layerMask = globalParams.cachedShadowRendererLayers;
                    cachedRendererListDesc.sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;
                    cachedRendererListDesc.renderQueueRange = RenderQueueRange.opaque;
                    RendererList cachedRendererList = context.CreateRendererList(cachedRendererListDesc);

                    if (cachedRendererList.isValid)
                    {
                        cmd.SetRenderTarget(cachedPlanarShadowmapTexs[hash], RenderBufferLoadAction.DontCare,
                            RenderBufferStoreAction.Store);
                        cmd.ClearRenderTarget(true, false, clearColor);
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                        cmd.DrawRendererList(cachedRendererList);
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                    }
                    else
                    {
                        Debug.LogError("CachedRendererList is invalid!");
                    }
                }
                
                cmd.SetRenderTarget(planarShadowmapTex, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                if (globalParams.shadowCache)
                {
                    // cmd.ClearRenderTarget(true, false, clearColor);
                    //Shadow Cache情况下，可以使用全屏Blit代替Clear
                    // cmd.Blit(cachedPlanarShadowmapTex, planarShadowmapTex);
                    CommonUtil.BlitCopyDepth(cmd, cachedPlanarShadowmapTexs[hash], planarShadowmapTex);
                }
                else
                {
                    cmd.ClearRenderTarget(true, false, clearColor);
                }
```

#### 5 总结

以上便是Planar Shadowmap的一些介绍与实现，其本身原理并不复杂，也因此性能比较好，尤其是对CPU端，减少了大量Shadow需要的预计算工作。当然，它的使用场景也非常有限，只适用于平面场景，所以比较适合MOBA和SLG类型的游戏。在我实践的过程中，本质上也算把它的坑踩了一遍，在方案中，依然存在很多未解决的问题，或解决的不好的问题，也希望大家能提出一些改进的建议。

#### 参考

1. 【Unity SLG大地图的绝佳阴影方案-平面阴影】https://zhuanlan.zhihu.com/p/688929547
2. 【使用顶点投射的方法制作实时阴影】https://zhuanlan.zhihu.com/p/31504088
3. 【Implementing Projected Planar Shadows in Unity】https://cosmicworks.io/blog/2019/02/implementing-planar-shadows-in-unity/
4. 【Unity平面阴影(王者荣耀阴影实现)】https://zhuanlan.zhihu.com/p/42781261
5. 【【Unity Shader】平面阴影（Planar Shadow）】https://zhuanlan.zhihu.com/p/266174847
6. 【Unity改造URP的CSM阴影】https://zhuanlan.zhihu.com/p/691367954
