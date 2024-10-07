# 【RecaNoMaho】从零开始的PCSS软阴影

#### 0 写在前面

RecaNoMaho是我的开源Unity URP项目，在其中我会致力于收录一些常见的、猎奇的、有趣的渲染效果与技术，并以源码实现与原理解析呈现，以让该系列内容具有一定的学习价值与参考意义。为了保证一定的可移植性、可迭代性和轻量性，在这些渲染效果与技术的实现中，第一原则是能写成RenderFeature的尽量写成RenderFeature，代价是原本一些修改URP代码很方便能做的事情可能要绕点路，以及丢失一些管线层面的可能优化点，这些优化可以在实际实践中再去实现。个人能力有限，文内如果有误或者有更好的想法，欢迎一起讨论~

RecaNoMaho项目仓库：https://github.com/recaeee/RecaNoMaho_P

目前使用的Unity版本：2022.3.17f1c1

目前使用的URP版本：14.0.9

在本文中，你可以了解到：

1. Percentage-Closer Soft Shadow的基本原理、在URP中的基本实践。
2. **半影遮罩Penumbra Mask**（PCSS优化）的简单实践。
3. 无痛了解Unity 6 HDRP中的改良版PCSS（无官方文档，包含大量个人理解）的实现与优化，其中包括**在CSM中的PCSS实现**、使用**Fibonacci Sprial Disk**动态调整PCSS采样性能、基于**角直径**的空间变换、**Cone-based Sample**消除自遮挡等等。
4. Unity 6 HDRP的改良版PCSS移植到2022版本的URP的实践（包括前向渲染和延迟渲染）。

以下是本文实现的PCSS软阴影效果。

![20241007232250](https://raw.githubusercontent.com/recaeee/PicGo/main/20241007232250.png)

![20241007232048](https://raw.githubusercontent.com/recaeee/PicGo/main/20241007232048.png)

#### 1 什么是PCSS（Percentage-Closer Soft Shadow）

在图形学中，有这么一个基础性的问题，我们**怎么让面光源产生软阴影**。软阴影，它的好处非常多，首先在我们真实世界中，大部分光源都是面光源，真实世界中的阴影大部分都是软的，也就是不存在一个非常清晰的轮廓区分开亮部和暗部，也就是说**软阴影是PBR所追求的一个效果**。另一方面，阴影的软硬程度也暗示了**物体之间的距离关系**，**当遮挡物与阴影接受物之间距离越远时，阴影越软**（越模糊），这也是真实世界中人们观察到的规律。

下图截取了GAMES 202中Lecture 3的片段，拍摄真实世界中的一支钢笔中的照片，可以看到，从纸面（**阴影接收处**）上的阴影处往**光源**方向看去，和钢笔（**遮挡物**）之间距离更近的地方，阴影更加锐利，而距离更远的地方，阴影更加柔软。

![20240903190130](https://raw.githubusercontent.com/recaeee/PicGo/main/20240903190130.png)

而下面这个片段中的软阴影效果，就是我们想要实现的软阴影效果，它符合我们上面说到的规律，**从阴影接受处往光源看去，与遮挡物之间距离越远时，阴影越软**。（从下面的右图中也可以简单看出软阴影形成的原因，因为受到遮挡物月亮的影响，有一部分地球上的阴影区域会受到面光源太阳部分的光照，从而使阴影变得柔软。）

![20240903190634](https://raw.githubusercontent.com/recaeee/PicGo/main/20240903190634.png)

那回到本节的标题，什么是PCSS？**PCSS是一种能实现感知正确的软阴影的技术**，其全称为**Percentage-Closer Soft Shadow**。它所实现的软阴影的规律就是从阴影接受处往光源看去，与遮挡物之间距离越远时，阴影越软。

#### 2 PCF（Percentage-Closer Filter）

在PCSS出现之前，人们其实已经在考虑如何去实现软阴影的效果了，那就是耳熟能详的**PCF（Percentage-Closer Filtering）**，尽管它并不是物理正确的。

我们知道硬阴影的实现非常简单，将Shading Point的坐标转换到光源空间下，采样一次Shadowmap，比较Shading Point在光源空间下的Z值是否小于Shadowmap采样值（不考虑Reversed-Z，0代表近平面），如果是则返回1，则表明Shading Point位于光照区域，如果否则返回0，表明位于阴影区域。可以看到我们得到的**阴影评估值只有0和1两种情况，这也就造成了非常硬的阴影边缘**。

而PCF实现软阴影的过程也非常简单，我们让评估阴影的返回值不再只返回0或1，而是**返回[0,1]之间的Float值**，就可以实现软阴影的效果了。为了返回[0,1]之间的Float值，PCF并不会只采样Shadowmap上的一个样本，而是会**采样4个最近的样本**（考虑PCF2X2的情况下），得到4个0或1值，然后对这4个值根据到Shading Point的距离做双线性插值，得到一个[0,1]之间的Float值，**这个Float值就从一定程度上表示了从Shading Point附近的一块邻域内看向光源，这块领域中有多少比例的面积不被遮挡**，如下图所示。

![20240904221137](https://raw.githubusercontent.com/recaeee/PicGo/main/20240904221137.png)

它确实没啥严谨的逻辑依据，但它就是能做出些软阴影的效果，这种软阴影并非是物理正确的，但总比没有好。这种**从Shadowmap中检索多个样本并将结果混合**的方法就称为Percentage-Closer Filtering，我们可以控制采样样本的数量来得到不同程度的软阴影，常见的比如PCF2X2、3X3、7X7等等。

显然PCF存在一些问题，第一是上面提到的它不讲道理，第二是我们对每个Shading Point都使用相同大小的采样区域（采样数量），导致**所有阴影区域都具有相同的软硬程度**，虽然在某些情况下我们可能可以接受，但对于追求更加物理的渲染效果来说，它是不够的。如下图所示，左边使用PCF，右边使用PCSS，明显右边的更加真实和符合直觉。

![20240904221642](https://raw.githubusercontent.com/recaeee/PicGo/main/20240904221642.png)

#### 3 PCSS的理论基础

为了解决上面提到的PCF软阴影的问题（不物理正确，所有阴影区域具有相同的软硬程度），在2005年Fernando提出了Percentage-Closer Soft Shadow，也就是PCSS。它实现了很重要的一点，那就是**根据Shading Point到遮挡物的平均距离决定PCF的采样区域大小**。换句话说，**对于不同的Shading Point，会使用不同大小的PCF采样区域**。

在PCSS中，我们考虑**光源Light**（以及其对应Shadowmap），**遮挡物Block**，**阴影接收物Receiver**，其实现过程可以分成3个步骤：

1. **Blocker Search Step（Blocker搜索步骤）**：首先，我们根据Shading Point寻找Shadowmap上的一个范围，在该范围中，我们求出**光源空间深度值比Shading Point近（即能够遮挡住Shading Point）的所有深度值的平均值$d_{Blocker}$**。

    ![20240905091529](https://raw.githubusercontent.com/recaeee/PicGo/main/20240905091529.png)

2. **Penumbra Size Estimation Step（半影尺寸估计步骤）**：假设Blocker、Receiver、Light平行的情况下，根据遮挡物到光源的平均值$d_{Blocker}$、阴影接收物到光源的距离$d_{Receiver}$、面光源面积$w_{Light}$来评估半影尺寸$w_{Penumbra}$，即**PCF的范围大小**。

    ![20240904225819](https://raw.githubusercontent.com/recaeee/PicGo/main/20240904225819.png)

    ![20240905092700](https://raw.githubusercontent.com/recaeee/PicGo/main/20240905092700.png)

3. **Percentage-Closer Filtering（PCF采样）**：根据第2步评估得到的PCF范围大小$w_{Penumbra}$，做经典的PCF采样实现软阴影。

PCSS的过程并不复杂，其核心步骤就是第1、2步，我们需要先求出遮挡物到光源的平均深度值，然后做一个评估，计算需要的PCF范围。

#### 4 PCSS的实践

##### 4.1 Blocker Search Step

第1步，我们需要计算**遮挡物到光源的平均深度值**$d_{Blocker}$。第一个问题是，**我们需要在Shadowmap上多大的搜索范围内求这个平均深度值**？

一种最直接的方法是根据相似三角形严格计算搜索范围。

考虑方向光的情况下，并且假定光源是正方形，光源视锥体是正方形，回看这张图，我们把Shadowmap放在光源视锥体的近平面，定义光源半径$r_{Light}$，ShadingPoint的光源空间深度下的$d_{Receiver}$，光源视锥体的近平面半径$r_{Near}$，那么根据相似三角形的关系，我们很容易得到需要在Shadowmap上求平均深度值的**搜索半径**$r_{Blocker}$（即下图中红色矩形的半径）。

![20240905091529](https://raw.githubusercontent.com/recaeee/PicGo/main/20240905091529.png)

![20240905205325](https://raw.githubusercontent.com/recaeee/PicGo/main/20240905205325.png)

但在实践中发现，我们采样Shadowmap得到的深度值，它并**不是光源空间深度值**，而是在光源空间下[NearPlane，FarPlane]到[0，1]的映射，要是想得到光源空间下的$d_{Receiver}$，还需要映射回去，这是一个开销点。另外，**方向光并不存在一个真实的光源位置**，NearPlane并不是根据光源位置得到的（而是根据阴影裁剪的CullingResult得到的），所以映射回去后，并不能严格认为是准确的$d_{Receiver}$。

考虑到以上点，我决定使用世界空间下固定的的半径，相比于使用Shadowmap上固定像素数量的半径，这样的好处是不受Shadowmap尺寸影响。

```hlsl
float BlockerSearchRadius()
{
    //搜索半径为世界空间下固定值
    //注意除以第一个CascadeSphere的半径，转换到第一级Cascade空间下的半径。后几级Cascade也统一使用该值，不再进一步考虑。
    return _BlockerSearchRadiusWS * _ShadowTileTexelSize / _CascadeShadowSplitSpheres0.w;
}
```

确定完$r_{Blocker}$，接下来考虑如何计算**遮挡物的平均深度值**，显然我们需要在Shadowmap上以Shading Point为中心，$r_{Blocker}$为半径的区域内采样若干个深度值，然后求平均。这里需要注意的一点是，我们求得是**遮挡物**的平均深度值，**如果采样点并未遮挡Shading Point（即采样点深度值大于Shading Point深度值），那么就不纳入考虑**。代码如下，返回值即遮挡物的平均深度，在代码中，使用了泊松圆盘采样对Sample的UV坐标做一个随机的偏移，以此得到更好的效果。

```hlsl
float FindBlocker(TEXTURE2D_PARAM(ShadowMap, sampler_ShadowMap), float2 shadowCoord, float receiverDepth, float searchRadius)
{
    float depthSum = 0.0;
    float depthCount = 0.0;
    float2 sampleCoord = float2(0.0, 0.0);
    float sampleDepth = 0.0;

    for(int i = 0.0; i < SAMPLE_COUNT; ++i)
    {
        sampleCoord = shadowCoord + poissonDisk[i] * searchRadius;
        sampleDepth = SAMPLE_TEXTURE2D(ShadowMap, sampler_ShadowMap, sampleCoord).r;
        
        if(sampleDepth > receiverDepth)
        {
            depthSum += sampleDepth;
            ++depthCount;
        }
    }

    return depthCount > FLT_EPS ? (depthSum / depthCount) : 0;
}
```

##### 4.2 Estimate Penumbra

接下来评估半影范围，也就是PCF的范围。其计算式在上文中提到过，对应的代码非常简单。

![20240905092700](https://raw.githubusercontent.com/recaeee/PicGo/main/20240905092700.png)

```hlsl
float EstimatePenumbra(float receiverDepth, float avgBlockerDepth, float lightRadius, float shadowMapSize)
{
    return shadowMapSize * (receiverDepth - avgBlockerDepth) * lightRadius / avgBlockerDepth;
}
```

##### 4.3 PCF

确定完Penumbra后，就是最后一步常规的PCF采样，其半径为Penumbra值。注意，在PCF采样中，我们也使用了**泊松圆盘采样**对每个Sample施加一个随机的偏移，以此达到更好的采样结果。在常规的PCF中，可能的做法是在半径内均匀采样，但是因为在PCSS中，PCF范围不固定，所以随机采样是个更好的选择。代码如下。

```hlsl
float PCF(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float3 shadowCoord, float filterRadius)
{
    float shadowAttenuationSum = 0.0;
    float3 sampleCoord = float3(0.0, 0.0, shadowCoord.z);
    for(int i = 0; i < SAMPLE_COUNT; ++i)
    {
        sampleCoord.xy = shadowCoord.xy + poissonDisk[i].xy * filterRadius;
        shadowAttenuationSum += SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, sampleCoord.xyz);
    }

    return shadowAttenuationSum / (float)SAMPLE_COUNT;
}
```

##### 4.4 小结

由此，经过计算遮挡物平均深度Blocker Search Step、评估半影范围Estimate Penumbra、PCF采样，我们便实现了PCSS。

最终得到ShadowAttenuation的函数如下。

```HLSL
float PCSS(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float3 shadowCoord, float4 shadowMapSize)
{
    float receiverDepth = shadowCoord.z;
    float blockerSearchRadius = BlockerSearchRadius();
    float lightRadius = LightRadius();
    //计算遮挡物平均深度值
    float avgBlockerDepth = FindBlocker(TEXTURE2D_ARGS(ShadowMap, sampler_LinearClamp), shadowCoord.xy, receiverDepth, blockerSearchRadius);
    //评估PCF范围
    float penumbra = EstimatePenumbra(receiverDepth, avgBlockerDepth, lightRadius, shadowMapSize.x);
    //PCF采样
    return PCF(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, penumbra);
}
```

如下图所示，以单Pixel 64次FindBlocker采样与64次PCF采样（合计128次采样）为例，我们得到了一个还不错的软阴影效果。

![20240911155056](https://raw.githubusercontent.com/recaeee/PicGo/main/20240911155056.png)

如果将采样次数改为16+16=32次，阴影的artifacts就会比较明显。

![20240911160814](https://raw.githubusercontent.com/recaeee/PicGo/main/20240911160814.png)

但显然单Pixel 128次采样代价太大了，接下来我们考虑如何优化PCSS。

##### 5 PCSS优化之路

##### 5.1 Penumbra Mask减少无效Sample

PCSS的代价非常高，一个Pixel需要非常高次数的纹理采样。但我们发现，**实际上很多不在半影区域的Pixel是根本不需要PCSS的**，它们的ShadowAttenuation非0即1。如下图所示，只有圈出来的范围内的Pixel需要做PCSS来实现软阴影。因此我们**可以在屏幕空间标记出哪些Pixel需要进行PCSS软阴影计算**，即**生成一张Penumbra Mask**。

![20240911162519](https://raw.githubusercontent.com/recaeee/PicGo/main/20240911162519.png)

另外，这里提到了我们是在屏幕空间生成Penumbra Mask，那么说到屏幕空间，也不难想到**这个方法在延迟渲染管线中是低成本的、友好的**，而在前向渲染管线中，为了生成这样一张Penumbra Mask是相对来说代价较大的，我们需要一个Depth Pre Pass来记录屏幕空间的深度值（这会使Draw Call翻倍），以此还原出屏幕像素对应片元的世界坐标，再变换到光源空间，对深度图进行采样，如果本身就用了Depth Pre Pass，那相对来说代价会小一些。

回到正题，**怎么认为屏幕空间上一个Pixel需要进行软阴影呢**？参考[《《原神》主机版渲染技术要点和解决方案》](https://zhuanlan.zhihu.com/p/356435019)的做法，主要思路是这样的：**对于屏幕空间上的一个Pixel，我们考察它周围的一片区域，如果该区域中每个Pixel都处于阴影中或都不处于阴影中**（硬阴影，单次采样得到ShadowAttenuation），**那么认为它不需要软阴影计算；反之，则需要进行软阴影计算**。

![20240911164650](https://raw.githubusercontent.com/recaeee/PicGo/main/20240911164650.png)

具体的做法如下，我们**生成一个1/4分辨率的Penumbra Mask**，这样**16个屏幕Pixel就对应了一个Mask值**，也就是说这16个像素要么都执行软阴影计算，要么都不执行。

对于Penumbra Mask上的每个Pixel（对应屏幕空间像素的4x4范围），**执行一定次数的Sample**，计算每个Sample点是否处于阴影中（ShadowAttenuation），然后将所有的Sample结果合并（ShadowAttenuation加权平均），如果加权结果为0或1，那么Mask值为0；如果加权结果位于(0，1)，Mask值为1。

然后**对Penumbra Mask纹理做一次Blur**，以此得到更大范围的半影区域。

![20240911164700](https://raw.githubusercontent.com/recaeee/PicGo/main/20240911164700.png)

最终，我们在Deferred Shading Pass中，只要对Mask不为0的区域做PCSS软阴影就行了。

接下来进入实践环节。

主要的实现思路也就和上面说的一样，首先分配一张**1/4 ColorAttachment分辨率**的R8UNORM纹理PenumbraMaskTex，然后FullScreenDrawCall一次，查询ColorAttachment每4x4区域内的像素的Mask值。在查询过程中，对于每4x4的区域，我使用了**4次硬件PCF采样**，这样可以捕获到4x4区域内所有像素信息。对于每次查询，首先**采样DepthAttachment还原出片元世界空间坐标**，然后**常规采样阴影贴图**，主要Shader代码如下。

```hlsl
    static float2 offset[4] = {float2(-1, 1), float2(1, 1), float2(-1, -1), float2(1, -1)};
    
    float PcssPenumbraMaskFrag(Varyings input) : SV_TARGET
    {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        float shadowAttenuation = 0;

        for(int i = 0; i < 4; ++i)
        {
            float2 coord = input.texcoord.xy + offset[i].xy * _ColorAttachmentTexelSize.xy;
            
            #if UNITY_REVERSED_Z
                float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, coord.xy).r;
            #else
                float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, coord.xy).r;
                deviceDepth = deviceDepth * 2.0 - 1.0;
            #endif
            
            float3 positionWS = ComputeWorldSpacePosition(coord.xy, deviceDepth, unity_MatrixInvVP);
            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);

            //注意考虑deviceDepth=0的情况下，我们认为shadowAttenuation为1。
            shadowAttenuation += 0.25f * lerp(1, SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, shadowCoord.xyz), step(Eps_float(), deviceDepth));
        }
        
        return shadowAttenuation < Eps_float() || shadowAttenuation > 1 - Eps_float() ? 0 : 1;
    }
```

以以下场景为例。

![20240913160935](https://raw.githubusercontent.com/recaeee/PicGo/main/20240913160935.png)

经过上一步得到的PenumbraMaskTex如下图所示。

![20240913161018](https://raw.githubusercontent.com/recaeee/PicGo/main/20240913161018.png)

然后为了**扩大半影区域**，**对PenumbraMaskTex进行高斯模糊**，这里直接借用了Bloom的高斯模糊算法，具体不放出来了，可以看我的项目代码或者原生Bloom的高斯模糊代码。经过高斯模糊后，得到的PenumbraMaskTex如下。

![20240913161221](https://raw.githubusercontent.com/recaeee/PicGo/main/20240913161221.png)

最后在PCSS的代码中，**根据片元的屏幕坐标对PenumbraMask采样一次**便可以知道当前片元是否需要执行PCSS。这里我直接用了if分支，PenumbraMask的值具有空间相关性，很容易让单个Warp内部都走到同一个分支，这时候可以避免分支语句带来的性能开销。

```hlsl
float PCSS(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float3 shadowCoord, float4 shadowMapSize, float2 screenCoord)
{
    float penumbraMask = SAMPLE_TEXTURE2D(_PenumbraMaskTex, sampler_LinearClamp, screenCoord);
    if(penumbraMask > Eps_float())
    {
        float receiverDepth = shadowCoord.z;
        float blockerSearchRadius = BlockerSearchRadius();
        float lightRadius = LightRadius();
        //计算遮挡物平均深度值
        float avgBlockerDepth = FindBlocker(TEXTURE2D_ARGS(ShadowMap, sampler_LinearClamp), shadowCoord.xy, receiverDepth, blockerSearchRadius);
        //评估PCF范围
        float penumbra = EstimatePenumbra(receiverDepth, avgBlockerDepth, lightRadius, shadowMapSize.x);
        //PCF采样
        return PCF(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, penumbra);
    }
    else
    {
        return SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord);
    }
}
```

将实际的半影范围标记出来，如下图所示，亮的区域为执行PCSS的半影区域，暗的区域为执行硬阴影1次采样的区域。。

![20240913161315](https://raw.githubusercontent.com/recaeee/PicGo/main/20240913161315.png)

由于Penumbra Mask是在屏幕空间估计的，硬阴影边界只是一条线，而Blur的范围是屏幕空间固定的，所以存在一个问题，**在摄像机距离物体过近时，因为Blur的范围有限制，所以半影区域会变小很多**。

![20240914144204](https://raw.githubusercontent.com/recaeee/PicGo/main/20240914144204.png)

参考[《Unity SRP 实战（三）PCSS 软阴影与性能优化》](https://zhuanlan.zhihu.com/p/462371147)，可行的一种处理方法是**在评估Enumbra时额外考虑片元到相机的距离，如果距离变近，则一定程度上增大Enumbra**。

##### 5.2 CSM下PCF采样防止越界

接下来做的一些优化参考了**Unity 6的HDRP中实现的PCSS算法**，主要代码在HDPCSS.hlsl中。

我们知道，在PCSS的PCF采样过程中，会在ShadowCoord周围一块区域进行多次采样，而我们的Shadowmap是基于CSM的，包含多级Cascade，因此采样可能发生**越界情况**，因此在做PCF时，我们需要考虑采样的边界情况，**每次采样和当前Cascade的边界做判断，如果越界，则不进行采样**。

为了在URP的Shader中读取到**每个Cascade在ShadowmapAtlas上的范围（Offset和Scale）**，我们需要修改下MainLightShadowCasterPass，把每个Cascade的ShadowSplitData每个Cascade的Offset和Scale传给GPU。

同时，在Shadows.hlsl的TransformWorldToShadowCoord函数中，返回的**shadowCoord的w分量**本来是空闲的，这里可以直接用来填充下PCSS要用到的CascadeIndex。

```hlsl
float4 TransformWorldToShadowCoord(float3 positionWS)
{
#ifdef _MAIN_LIGHT_SHADOWS_CASCADE
    half cascadeIndex = ComputeCascadeIndex(positionWS);
#else
    half cascadeIndex = half(0.0);
#endif

    float4 shadowCoord = mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));

    return float4(shadowCoord.xyz, cascadeIndex);
}
```

最后PCF采样时，每次Sample判断下是否越界即可。

```hlsl
float PCF(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, float filterRadius, int sampleCount
                                , float2 shadowmapInAtlasSize)
{
    float shadowAttenuationSum = 0.0;
    float sampleSum = 0.0;
    float3 sampleCoord = float3(0.0, 0.0, shadowCoord.z);

    //当前片元所处Tile的边界
    int cascadeIndex = (int)shadowCoord.w;
    float2 shadowmapInAtlasOffset = _CascadeOffsetScales[cascadeIndex].xy;
    float2 shadowmapInAtlasScale = _CascadeOffsetScales[cascadeIndex].zw;
    float2 minCoord = shadowmapInAtlasOffset;
    float2 maxCoord = shadowmapInAtlasOffset + shadowmapInAtlasScale;

    ...
    
    for(int i = 0; i < sampleCount; ++i)
    {
        ...

        //只有sampleCoord不超出Tile时执行采样
        if(!any(sampleCoord < minCoord) || any(sampleCoord > maxCoord))
        {
            shadowAttenuationSum += SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, sampleCoord.xyz);
            sampleSum += 1.0;
        }
    }

    return shadowAttenuationSum / sampleSum;
}
```

同样，FindBlocker的时候，我们也需要考虑越界情况。

##### 5.3 Fibonacci Spiral Disk斐波那契旋盘采样

在此前，我们使用PoissonDisk实现在一定半径范围内在均匀分布的点上进行采样，而在Unity 6 HDRP的PCSS中使用了**Fibonacci Spiral Disk**，它是基于斐波那契点集生成的圆盘均匀分布，对应参考文献[《Spherical Fibonacci Point Sets for Illumination Integrals》](https://people.irisa.fr/Ricardo.Marques/articles/2013/SF_CGF.pdf)。

Fibonacci Sprial Disk的**原始序列FibonacciSpiralDirection是一个单位方向序列**，即序列中每个元素都表达了一个单位方向，并且整个序列组成了**单位圆上均匀分布的方向向量**。FibonacciSpiralDirection可以预计算。

而为了实现在**固定半径**的圆盘内均匀采样，我们可以将FibonacciSpiralDirection中的方向sampleDirection乘以**固定半径上的均匀分布sampleRadius**，得到采样点。固定半径上的均匀分布可以直接根据当前sampleIndex除以sampleCount得到。

```hlsl
//使用FibonacciSpiralDisk做随机采样，它是一个Uniform的数组，元素只代表偏移的方向，而偏移的距离通过sampleIndex和sampleCount确定
float2 ComputeFibonacciSpiralDiskSampleUniform(const in int sampleIndex, const in float sampleCountInverse, const in float sampleBias,
                                out float sampleDistNorm)
{
    //MAD指令
    sampleDistNorm = (float)sampleIndex * sampleCountInverse + sampleBias;

    sampleDistNorm = sqrt(sampleDistNorm);

    return fibonacciSpiralDirection[sampleIndex] * sampleDistNorm;
}
```

该圆盘采样的好处在于，当我们动态调整sampleCount（不超过预计算好的FibonacciSpiralDirection数组长度），都可以从序列中取出均匀分布内的一些单位方向，并且根据sampleCount和sampleIndex指定距离，得到一个对于sampleCount动态变化的圆盘分布，即**可以根据采样数量动态生成圆盘分布**，也就是**可以在运行时去调整sampleCount，而不会影响采样的分布质量和性能**，可以**动态调整PCSS的采样性能**。

##### 5.4 基于角直径的BlockerSearchRadius计算

###### 5.4.1 角直径的引入

首先，我们回看下**确定搜索范围**的图示，我们能观察到主要是去**求由红色虚线组成的四棱锥与放置于光源视锥体近平面上的Shadowmap相交区域的半径**。换个角度来看这个问题，其实我们是**站在Shading Point上，然后看向光源，光源在我们看到的画面上所占的面积**。

![20240905091529](https://raw.githubusercontent.com/recaeee/PicGo/main/20240905091529.png)

我们再回看之前我们遇到的问题，求搜索范围的公式如下，需要考虑到面光源的半径$r_{Light}$以及Shading Point深度信息、光源空间近平面信息$d_{Near}$。在实践中，Shading Point深度信息我们是很好求的，但**方向光源的半径$r_{Light}$以及$d_{Near}$是比较难去确定的**。因此在之前，我们**粗暴地直接用世界空间下一个固定的搜索半径**，这确实太粗暴了，因此我们参考下Unity6 HDRP的做法。

回看下式，对于方向光源，我们把它**拆成两项$r_{Light}/d_{Receiver}$和$d_{Receiver}-d_{Near}$**。对于方向光源，我们假定$d_{Near}$为0，$d_{Receiver}-d_{Near}$这项我们很好求，那么问题就变成了**怎么求$r_{Light}/d_{Receiver}$**。

![20240905205325](https://raw.githubusercontent.com/recaeee/PicGo/main/20240905205325.png)


$r_{Light}/d_{Receiver}$是啥呢，回到我上面说到的换位思考，**站在Shading Point上，然后看向光源，光源在我们看到的画面上所占的面积**，没错，它就是$r_{Light}/d_{Receiver}$，它反映的就是**观察深度以及观察面积之间的关系**。而对于$r_{Light}/d_{Receiver}$，其实我们有一个很好的方法去定义以及计算，通过**角直径Angular diameter**，通过这个方法，我们完全不用去考虑光源在世界空间下的实际面积。接下来说说**角直径的定义和通过角直径去求$r_{Light}/d_{Receiver}$的过程**。

###### 5.4.2 角直径Angular diameter

对于方向光源来说，它实际表达的通常来说是**太阳**这些恒星，而太阳它的半径，在世界空间下，毫无疑问是非常大的，我们在确定搜索范围时直接去考虑太阳的半径是不合适的。在天文学上，天空中物体的大小通常都是根据**从地球上观测所见到的角直径**来描述，而很少用到真实的直径。

因此，对于方向光源的$r_{Light}$，我们有个更好的评估项——**角直径Angular diameter**。通过以下图示就可以很直观地理解角直径所表达的意义。

![20240922012335](https://raw.githubusercontent.com/recaeee/PicGo/main/20240922012335.png)

角直径的计算式如下，很简单，不做多解释。

![20240922012735](https://raw.githubusercontent.com/recaeee/PicGo/main/20240922012735.png)

###### 5.4.3 遮挡物平均搜索范围的计算

理解完角直径的概念以及公式，我们发现上图中的$d$的一半其实就是我们要求的$r_{Blocker}$，**当我们知道光源的角直径$\delta$，ShadingPoint的光源深度$D$，那么我们就能求$r_{Blocker}$了**，一个tan计算罢了，由此我们便确定了在计算遮挡物平均深度时的搜索范围了。

好吧，对于这个问题感觉我说了很多废话，可能会绕晕，但对于理解这个问题来说，我感觉是有必要展开的。

在实践中，对于光源我们新增一个**参数angularDiameter**，$tan_{\delta}$就代表了$r_{Light}/d_{Receiver}$，另外需要注意下光源空间中处理X和Z方向上的非均匀缩放。

确定完计算遮挡物平均深度时的搜索范围，之后就可以进行FindBlock，计算出遮挡物平均深度了。

```c#
//CPU侧
float lightAngularDiameter = globalParamspcssLightParams.dirLightAngularDiameter;
float dirlightDepth2Radius =
    Mathf.Tan(0.5f * Mathf.Deg2Rad * lightAngularDiameter);
for (int i = 0; i < shadowCascadeCount; ++i)
{
    float shadowmapDepth2RadialScale = Mathf.Abs(PcssContext.deviceProjectionMatrixs[i].m00 /
                                                 PcssContext.deviceProjectionMatrixs[i].m22);
    pcssCascadeDatas[i].dirLightPcssParams0.x = dirlightDepth2Radius * shadowmapDepth2RadialScale;
    pcssCascadeDatas[i].dirLightPcssParams0.y = 1.0f / pcssCascadeDatas[i].dirLightPcssParams0.x;
    ...
}
cmd.SetGlobalVectorArray(ShaderConstants._DirLightPcssParams0, dirLightPcssParams0);
```

```hlsl
//GPU侧
float receiverDepth = shadowCoord.z;
float depth2RadialScale = _DirLightPcssParams0[cascadeIndex].x;
float blockerSearchRadius = BlockerSearchRadius(receiverDepth, depth2RadialScale);

...

float BlockerSearchRadius(float receiverDepth, float depth2RadialScale)
{
    #if UNITY_REVERSED_Z
        return (1.0 - receiverDepth) * depth2RadialScale;
    #else
        return receiverDepth * depth2RadialScale;
    #endif
}
```

##### 5.5 基于角直径的Estimate Penumbra近似

我们再观察下原本的遮挡物平均深度计算式和Estimate Penumbra的计算式，发现他们俩很相似，因为本质都是**求相似三角形**嘛。这里就需要我们思考下了，Penumbra计算式中的$w_{Light}/d_{Blocker}$这一项是不是有点熟悉又陌生，长得很像$r_{Light}/d_{Receiver}$，也就是半角直径的tan值，但$d_{Receiver}$变成了$d_{Blocker}$。这里就到了图形学中绕不开的一个课题了，**近似**！可以认为方向光源到遮挡物的距离和方向光源到人眼的距离是同一个数量级的，而$r_{Light}$是另一个数量级的。

![20240905205325](https://raw.githubusercontent.com/recaeee/PicGo/main/20240905205325.png)

![20240904225819](https://raw.githubusercontent.com/recaeee/PicGo/main/20240904225819.png)

由此，**$w_{Penumbra}$的值也可以直接由$(d_{Receiver}-d_{Blocker})*tan\delta$确定**。

```hlsl
float EstimatePenumbra(float receiverDepth, float avgBlockerDepth, float depth2RadialScale)
{
    if(avgBlockerDepth < Eps_float())
    {
        return 0;
    }
    else
    {
        return abs(avgBlockerDepth - receiverDepth) * depth2RadialScale;
    }
}
```

##### 5.6 改良遮挡物平均深度计算FindBlocker

###### 5.6.1 基于角直径的SampleUV Offset

5.4节和5.5节都可以说是通过引入角直径来进行光源空间深度值到Shadowmap上Radius的转换，接下来讲讲Unity6 HDRP PCSS中**对Sample做出的优化**。我们知道PCSS中有两步要进行Sample，一步是计算遮挡物平均深度FindBlocker，另一步是PCF。首先我们来看看FindBlocker阶段。

首先回顾下原始的FindBlocker步骤，我们是如何去计算avgBlockerDepth的。**我们先计算出搜素的半径BlockerSearchRadius，再在Shadowmap上对Shading Point周围做基于TexelSize的偏移（poissonDisk）采样深度值，计算遮挡物平均深度**。

而在5.3节中提到，我们将Poisson Disk优化为了Fibonacci Spiral Disk，这是对UV偏移使用的噪声做出的优化。而同时，我也发现它**将偏移从基于TexelSize改为了基于DepthToRadial，也就是基于角直径**。这里我自己没理解多透彻，个人理解如下，我们一般的思路是$TexelSize*Radius*NormalizedOffset$对采样点做偏移，而角直径(考虑投影矩阵XZ方向比例后)中已经包含了Texel的信息（即基于TexelSize），因此变成$Radius * NormalizedOffset$，而$Radius=f(depth,depth2radial)$，其中depth2radial考虑了角直径以及投影矩阵XZ方向比例（即基于角直径）。读者可以进一步参考Unity 6 HDRP的PCSS源码。

```hlsl
float2 ComputeFindBlockerSampleOffset(const in float filterRadius, const in int sampleIndex, const in float sampleCountInverse, const in float clumpExponent,
                                const in float2 sampleJitter, const in float2 shadowmapInAtlasScale,
                                out float sampleDistNorm)
{
    float2 offset = ComputeFibonacciSpiralDiskSampleClumped(sampleIndex, sampleCountInverse, clumpExponent, sampleDistNorm);
    //增加Temporal Jitter
    offset = float2(offset.x * sampleJitter.y + offset.y * sampleJitter.x,
                        offset.x * -sampleJitter.x + offset.y * sampleJitter.y);
    //应用SearchRadius，SearchRadius已经经过depth2Radial转换，即基于角直径，不再考虑TexelSize
    offset *= filterRadius;
    //应用shadowmapInAtlasScale=当前Tile尺寸(2048)*整个ShadowAtlas的Texel大小(1/4096)=0.5，角直径是基于单个Tile的，因此要考虑Atlas上的缩放
    offset *= shadowmapInAtlasScale;

    return offset;
}
```

从代码中也可以看到对于SampleUV还存在一个**temporal Jitter**，它是一个基于frameIndex生成的Jitter，常规的Temporal Filter，这里不过多赘述，代码如下。

```hlsl
float2 ComputePcfSampleJitter(float2 pixCoord, uint frameIndex)
{
    float sampleJitterAngle = InterleavedGradientNoise(pixCoord, frameIndex) * 2.0 * PI;
    return float2(sin(sampleJitterAngle), cos(sampleJitterAngle));
}
```

###### 5.6.2 Cone-Based Zoffset

除了对SampleUV进行了基于角直径的Offset，Unity 6 HDRP的PCSS也**对每次采样深度值做比较时的Shading Point的深度值也做了偏移**，并且是**基于锥形的偏移**（Cone-Based）。

官方注释对这种改良的PCSS做出的解释如下。

This is a modified PCSS. Instead of performing both the blocker search and filtering phases using a flat disc of samples centered around the shaded point, it adds a z offset to sample points extruding them in a cone shape towards the light. The base of the cone is at infinity, the apex at the shaded point, and samples lie on the surface of the cone.

 The idea is that only casters within the volume of that pyramid would contribute to the shadow. In other words any casters caught by a sample with z further away from the light than z of that sample don't contribute to the shadow.

The maximum height of the pyramid is the z distance between the shaded point and a specified parameter, this clamps the penumbra size and also avoid inconsistencies between cascades shall the distance exceed the near plane of a cascade because all occluders behind the near plane are clamped and are considered closer to the receiver than they are.  As the cascade frustum can dynamically change, we cannot use the near plane and it is thus best to use a fixed distance as tuning parameter so the penumbra size is at least consistent within each cascade. Higher maxSampleZDistance values result in wider penumbras.

由于个人并未找到相关的资料文献，因此只能从代码中解读该方法所作的事情。简单来说，在FindBlocker阶段，我们会**在每次采样深度图后，对原本Shading Point的深度值做一个偏移zoffset**，并且这个**offset随着采样点与Shading Point之间距离增加而增加**（可以线性也可以指数），从画图看来就形成了一个**锥形**。最后我们让从Shadowmap中采样得到的sampleDepth与receiver depth with zoffset作比较，然后计算遮挡物平均深度。由此可见，**红色区域内的遮挡物就不会对FindBlocker做出贡献**，通过这样的方法来**减轻自遮挡**。

![4496a622bd1eb2596e1edb1abb9d09f2_720](https://raw.githubusercontent.com/recaeee/PicGo/main/4496a622bd1eb2596e1edb1abb9d09f2_720.png)

以下为核心代码。

```hlsl
float FindBlocker(TEXTURE2D_PARAM(ShadowMap, sampler_ShadowMap), float2 shadowCoord, float receiverDepth, float searchRadius, float2 minCoord, float2 maxCoord, int sampleCount, float clumpExponent, float2 shadowmapInAtlasScale, float2 sampleJitter)
{
#if UNITY_REVERSED_Z
    #define Z_OFFSET_DIRECTION 1
#else
    #define Z_OFFSET_DIRECTION (-1)
#endif
    float depthSum = 0.0;
    float depthCount = 0.0;
    float sampleCountInverse = rcp((float)sampleCount);

    for(int i = 0; i < sampleCount && i < DISK_SAMPLE_COUNT; ++i)
    {
        float sampleDistNorm;
        float2 offset = ComputeFindBlockerSampleOffset(searchRadius, i, sampleCountInverse, clumpExponent, sampleJitter, shadowmapInAtlasScale, sampleDistNorm);
        float2 sampleCoord = shadowCoord + offset;
        float sampleDepth = SAMPLE_TEXTURE2D_LOD(ShadowMap, sampler_ShadowMap, sampleCoord, 0.0).r;

        //对阴影接受物的Z做Cone Base偏移，这对于消除自遮挡很重要
        float radialOffset = searchRadius * sampleDistNorm;
        float zOffset = radialOffset;
        float receiverDepthWithOffset = receiverDepth + (Z_OFFSET_DIRECTION) * zOffset;

        if(!(any(sampleCoord < minCoord) || any(sampleCoord > maxCoord)) &&
#if UNITY_REVERSED_Z
            (sampleDepth > receiverDepthWithOffset)
#else
            (sampleDepth < receiverDepthWithOffset)
#endif
            )
        {
            depthSum += sampleDepth;
            depthCount += 1.0;
        }
    }

    return depthCount > FLT_EPS ? (depthSum / depthCount) : 0;
}

//FibonacciSpiralDisk随机采样，Sample点更集中在中心，更适合给FindBlocker用
float2 ComputeFibonacciSpiralDiskSampleClumped(const in int sampleIndex, const in float sampleCountInverse, const in float clumpExponent,
                                out float sampleDistNorm)
{
    //sampleDistNorm的计算在这里
    sampleDistNorm = (float)sampleIndex * sampleCountInverse;

    sampleDistNorm = PositivePow(sampleDistNorm, clumpExponent);

    return fibonacciSpiralDirection[sampleIndex] * sampleDistNorm;
}
```

##### 5.7 PCF Cone-Based Zoffset

和5.6.2节类似，除了在FindBlocker阶段使用Cone-Based Zoffset，**在PCF阶段，同样对shadowCoord.z进行Cone-Based Zoffset**，其中的偏移映射关系有略微的不同。具体不过多解释了，代码也是类似的，这里也不贴了。当然，这里也是为了**消除自遮挡**。

#### 6 最终PCSS效果展示

以24次FindBlocker采样+16次PCF采样的PCSS软阴影效果如下。

![20241007232209](https://raw.githubusercontent.com/recaeee/PicGo/main/20241007232209.png)

![20241007232400](https://raw.githubusercontent.com/recaeee/PicGo/main/20241007232400.png)

以64次FindBlocker采样+64次PCF采样（伪超高质量）的PCSS软阴影效果如下。

![20241007232250](https://raw.githubusercontent.com/recaeee/PicGo/main/20241007232250.png)

![20241007232048](https://raw.githubusercontent.com/recaeee/PicGo/main/20241007232048.png)

实际上，由于截图会抑制Temporal Filter的效果，在实机中，24+16次的效果可以接近64+64次的效果，Temporal Filter太好用了。

再来个更大范围的软阴影效果。

![20241007232602](https://raw.githubusercontent.com/recaeee/PicGo/main/20241007232602.png)

#### 7 小结

好啦，以上便是本篇的所有内容了。首先，我们了解并实践的最基础的PCSS，然后对PCSS进行了优化，参考《原神》的思路使用Penumbra Mask减少无效的Sample，参考Unity 6 HDRP的PCSS进行了算法的改良，PCSS在CSM下防止越界，使用斐波那契旋盘采样，使用角直径描述方向光源与进行空间变换，Penumbra的近似，对ShadingPoint进行Cone-Based Zoffset消除自遮挡，引入Temporal Jitter等等，最终也相当于将Unity 6 HDRP的改良版PCSS移植了过来（并且同时支持前向渲染和延迟渲染），并且对这些优化进行了解读。个人感觉PCSS的理论虽然简单，但在实践中其实有很多可以做的改进，感觉也存在很多我没了解到的优化点。同时，本文中包含大量个人理解，如有不对，请多指出与纠正~

好久没更新啦~这段时间忙很多事情，想很多事情，耽搁了挺久，感觉逐渐遁入了虚无主义（变懒了），自我感触挺多。但生活还是要继续前进，多学习实践，争取多更新！

#### 参考
1. 【Unity SRP 实战（三）PCSS 软阴影与性能优化】https://zhuanlan.zhihu.com/p/462371147
2. 【Percentage-Closer Soft Shadows】https://developer.download.nvidia.cn/shaderlibrary/docs/shadow_PCSS.pdf
3. 【GAMES202-高质量实时渲染 Lecture3 Real-time Shadows 1】https://www.bilibili.com/video/BV1YK4y1T7yY?p=3&vd_source=ff0e8ecb1d7ea963eef228f6c1cc6431
4. 【Real-Time Rendering, Fourth Edition】
5. 【GAMES_101_202_Homework】https://github.com/DrFlower/GAMES_101_202_Homework/tree/main
6. 【实时阴影技术（1）Shadow Mapping】https://www.cnblogs.com/KillerAery/p/15201310.html#penumbra-mask
7. 【《原神》主机版渲染技术要点和解决方案】https://zhuanlan.zhihu.com/p/356435019
8. 【角直径Angular diameter】https://en.wikipedia.org/wiki/Angular_diameter
9. 【Spherical Fibonacci Point Sets for Illumination Integrals】https://people.irisa.fr/Ricardo.Marques/articles/2013/SF_CGF.pdf