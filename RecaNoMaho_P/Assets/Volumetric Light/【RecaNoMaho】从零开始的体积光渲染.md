# 【RecaNoMaho】从零开始的体积光渲染

![b08660e53624335d34a65c7a8b1f9a13](https://raw.githubusercontent.com/recaeee/PicGo/main/b08660e53624335d34a65c7a8b1f9a13.png)

#### 0 写在前面

RecaNoMaho是我的开源Unity URP项目，在其中我会致力于收录一些常见的、猎奇的、有趣的渲染效果与技术，并以源码实现与原理解析呈现，以让该系列内容具有一定的学习价值与参考意义。为了保证一定的可移植性、可迭代性和轻量性，在这些渲染效果与技术的实现中，第一原则是能写成RenderFeature的尽量写成RenderFeature，代价是原本一些修改URP代码很方便能做的事情可能要绕点路，以及丢失一些管线层面的可能优化点，这些优化可以在实际实践中再去实现。个人能力有限，文内如果有误或者有更好的想法，欢迎一起讨论~

RecaNoMaho项目仓库：TODO(Public URL)

目前使用的Unity版本：2022.3.17f1c1

目前使用的URP版本：14.0.9

![b08660e53624335d34a65c7a8b1f9a13](https://raw.githubusercontent.com/recaeee/PicGo/main/b08660e53624335d34a65c7a8b1f9a13.png)

（先贴上目前在RecaNoMaho中的体积光渲染效果图）

#### 1 什么是体积光？

在现实环境中，我们有时候会看到光路从云之间洒下来，在带有一些烟雾的舞台上可以看到聚光灯下具有“体积”的光，这种现象名为**丁达尔效应Tyndall effect**，又称丁达尔现象、延得耳效应（也被广大网友玩梗无数称作各种XXX效应，如达尔文效应等），其原理是**光被悬浮的胶体粒子散射**。下图中德国宫廷主教座堂内被散射而显现的光路，是丁达尔效应的一个典型例子。

![20240319102316](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240319102316.png)

那体积光是什么？**体积光是3D计算机图形学中用于为渲染的场景添加照明效果的一种技术**，它可以让我们看到“空间中的光束”，被广泛应用于渲染和游戏领域，如下图所示，是CG技术中的体积光渲染效果。

![20240320185808](https://raw.githubusercontent.com/recaeee/PicGo/main/recaeee/PicGo20240320185808.png)

那么，我们便可以得出，**体积光是一种CG技术，用于模拟现实环境中的丁达尔效应，或者说是用来模拟微粒对光的散射**。因此，为了实现体积光，我们首先需要简单了解下**现实环境中的丁达尔效应的成因**，参考《Real-Time Rendering, Fourth Edition》第14章的内容，接下来我会进行通俗地解释（有兴趣的可以直接看原书）。

##### 1.1 丁达尔效应成因

首先，我们需要思考一下，对于丁达尔效应，我们看见的**具有“体积”的光究竟是什么**？

我们知道之所以我们能看到现实中的各种实体表面，是因为光经过实体表面（或者从实体表面出发，即自发光），并发生反射、折射等行为进入人眼。而对于丁达尔效应，也是同理，但是区别在于光经过的不是“实体表面”，而是**一片充满微粒的介质**（区域，或者说空间），称为**参与介质Participating media**。好比光经过“实体表面”时会发生反射、折射，**当光经过参与介质时会发生散射和吸收两种行为**（更严谨地说，反射和折射都属于散射行为），我们看到的具有“体积”的光的成因就是，**光经过参与介质后通过散射进入人眼**，这也就是丁达尔效应的成因。

##### 1.2 参与介质

接下来说说**参与介质**。

**参与介质Participating media用于描述充满颗粒的体积，它们是参与光线传输的介质，换句话说，它们通过散射作用或者吸收作用，来影响穿过它们的光线**。其实，实体表面比如石头、木头也属于参与介质，它们是**密度很大的参与介质**，**大部分光线**经过它们的表面时都会**接触到参与介质中的粒子**并**发生散射**（散射程度高，主要是几何散射），人眼看到的就是这些大量散射而来的光。而像水、雾、蒸汽这类**密度较小的参与介质**，因为总粒子数少，**大部分光线**经过介质时受不到其中粒子的影响，只有**少部分光线受到影响会发生散射**（大部分光线会以原方向前进），如果我们并没有去直视光线来向，那么大部分光线几乎无法进入人眼，因此我们就看不到这些光，但**的确会有小部分光线会受到参与介质中粒子影响发生散射**，而当这部分散射光进入人眼，就会让我们看到这些密度较小的参与介质（水、雾、蒸汽）中的光。

打个比方来说，如下图所示，（左）密度较大的参与介质（比如石头）接收到**100%的光**，其中**99.999%的光**都会散射到周围各个方向，可能只有**0.001%的光**会透射过去按原方向射出；而对于（右）密度较小的的参与介质（比如云雾）接收到**100%的光**，其中**70%的光**会透射过去并按原方向前进，同时**30%的光**（蓝色箭头表示）被散射到其他方向上，当人眼接收到这部分**散射出去的光**，就看到了云雾中的**丁达尔效应**。注意，**不用去关注**图中左右对比下散射出的的光的偏转量差别，在目前只需要考虑光的透射与散射占比（之所以在绘图时考虑到不同介质对散射光的偏转量，是为了铺垫在后文中介质粒子对光的散射的影响的直觉）。

![不同参与介质对光的散射2.drawio](https://raw.githubusercontent.com/recaeee/PicGo/main/不同参与介质对光的散射2.drawio.png)

由此，我们知道了什么是参与介质，并且知道了不同密度的参与介质对光的散射程度不同。再补充一点，人眼通常看到的丁达尔效应，可以认为包含两部分光，一部分是上文中提到的**光源经过低密度参与介质散射出的光**（也就是我们觉得很美的光芒 ），另一部分是**其他参与介质（相对于视线的后方的）实体表面射出的经过参与介质透射出的光**（即透过丁达尔效应看到的实体表面，比如云雾后的高山），如下图所示。也正是因为低密度参与介质中粒子数量较少，所以被散射的光总量较少，我们才能看清它背后的东西。

![丁达尔效应光组成.drawio](https://raw.githubusercontent.com/recaeee/PicGo/main/丁达尔效应光组成.drawio.png)

##### 1.3 光的散射与吸收

好了，讲完参与介质，接下来讲讲**光的散射与吸收**。

**光线穿过介质并被介质中的粒子所反射，这类事件通常会被称为光线散射Light scattering**（注意这里说的反射，并不是意味着出射方向一定要和入射方向夹角很大，任意角度的出射都可以认为是反射）。**任何事物都会发生散射**。通俗意义上的表面反射与折射都属于光的散射。

如果要详细说光的散射相关的原理，其实非常复杂。但对于体积光渲染，我们不需要了解那么多，我们只需要知道：**当光线经过参与介质中极小的一块区域时，它会以一定概率被散射到（与原方向）不同的方向上**。以上便是**支撑体积光的理论基础**，至于它的定量模型，我们在第2章再来细说，包括我们怎么定义每个方向上散射出多少光，有哪些影响参数。

至于参与介质中的粒子到底是怎么让光偏转的，我们并不需要了解很详细（当然有兴趣的同学可以自行了解，RTR4的第9章）。

同时，我们还要知道，**光（子）经过参与介质时会有一部分被吸收，并转化为热量或者其他形式的能量**，这也是体积光渲染中**要考虑到的一个因素**。

##### 1.4 影响散射的因素

**为什么我们只有偶尔或者特定环境下能看到现实中的丁达尔效应？**

首先，思考一下什么情况下我们容易看到现实中的丁达尔效应？想象一下，布满灰尘的教堂，早上布满晨雾的森林，雨刚停后放晴的天空。这些环境的特点都是，**参与介质的密度到达一定程度**。也就是说，**参与介质的密度越大，其中的粒子就越多，更多的粒子就会导致更多的散射，也可以说散射程度越大**。

同时，**参与介质中单个粒子大小也会影响散射**，**当光线经过参与介质中的单个粒子，它在不同方向上的散射概率和粒子半径有关**（即Phase function，后面会提到）。这是直观上比较难理解的一点，但它实际上是渲染体积光的一个重要参数，因为现实环境中的丁达尔效应可以在很多参与介质中发生，比如水、云雾、灰尘的房间，虽然都是丁达尔效应，但这些场景下的实际现象还是会有所区别（偏细节上的区别），而其原因之一就是因为**这些场景下参与介质的粒子不同，也就意味着粒子半径不同**。

再其次，**入射光的波长也会对散射造成影响**，波长更短的光更容易被散射，蓝色光的波长更短，更容易被散射，这也就是为什么天空是蓝色的原因之一。但是，实际上在实时渲染领域，我们**很少会将这部分要素考虑在内**，除非是在对物理模拟非常严谨的环境下（比如大气渲染的瑞利散射）。

总结一下，**对于体积光渲染所必须的，影响光的散射的因素主要有2点**：

1. **参与介质密度**，密度越大，散射的粒子越多，散射程度越强。
2. **组成参与介质的粒子的半径**，不同半径的粒子在各方向散射概率不同，可以区分出体积光渲染模拟的不同环境，比如水中的体积光、烟雾中的体积光。

注意，**光的波长对散射的影响被我们忽略**，在实时渲染环境下考虑它的复杂度较高。

#### 2 体积光的定量模型

##### 2.1 影响光线传播并穿过介质的4类事件

在第1节，我们了解了丁达尔效应的成因是光经过参与介质后通过**散射**进入人眼，并且**定性**地描述了**参与介质对光的散射的影响要素**。接下来，就到了**体积光的定量模型**了，也就是**将物理规律转化为数学公式**，之后再在渲染实践中将数学公式转化为代码。

这一节的内容依然参考《Real-Time Rendering, Fourth Edition》。首先，我们知道在实时渲染中，对于3D模型的渲染方法，比如PBR，通常来说最后计算的都是光线从光源出发，打到实体表面反射再进入到摄像机每个像素的RGB值。使用原书中的话更严谨地说，我们会**计算从表面着色点到相机位置的radiance**。那么对于体积光的渲染，也是类似的，我们需要**计算光线从光源出发，打到参与介质再散射进入到摄像机每个像素的RGB值**，更严谨地说，我们要**计算沿着光线传播并穿过参与介质到相机位置的radiance**。

那么问题就变成了这个radiance怎么算。答案直接参考原书（只考虑**单次散射**），当然同学们也可以直接去看原书的14.1.1节，篇幅有限，内容以概括为主，原书讲的更详细。

**有4种类型的事件可以影响沿着光线进行传播并穿过介质的radiance**。下图种给出了这些函数的图示说明：

![20240322235347](https://raw.githubusercontent.com/recaeee/PicGo/main/20240322235347.png)

可以将其总结为：

1. **吸收Absorption**（$\sigma_a$的函数）——光子被介质吸收并转化为热量或者其他形式的能量。

2. **外散射Out-scattering**（$\sigma_s$的函数）——光子被介质中的粒子反弹，并从介质中散射出去。该事件的发生概率，取决于描述光线反射方向分布的相位函数$p$（Phase function）。

3. **发射Emission**——当介质达到较高温度时，例如火焰的黑体辐射，可以从介质中发射光线。（通常在渲染中我们不需要考虑这部分）

4. **内散射In-scattering**（$\sigma_s$的函数）——来自任何方向的光子，在被介质粒子反弹之后都可以散射到当前的光路中，并对最终的radiance产生一定的贡献。在给定方向上内散射进来的光量，也取决于该光线方向的相位函数$p$。（不难理解，与外散射相对应，有光线从当前路线被散射出去，当然也会有光线从其他路线散射到当前路线）

根据以上这4类事件，在一个路径中加入光子是**内散射$\sigma_s$和发射（通常可忽略）的函数**，也就是增加一个光路上的RGB值；而移除光子则是**消光系数(extinction)$\sigma_t=\sigma_a+\sigma_s$的函数**，代表吸收和外散射。

##### 2.2 如何计算最终的RGB值

最终对于体积光渲染，**最后的radiance**（即相机最终单个像素的RGB值）由两部分组成，一部分是**从实体表面反射的光线经过介质到摄像机位置的radiance**，另一部分是**从来自精确光源的散射光线经过介质到摄像机位置的radiance**，公式如下，看起来很复杂，其实不难理解（数学公式真是精简抽象到让人感叹的地步）。

![QianJianTec1711125017991](https://raw.githubusercontent.com/recaeee/PicGo/main/QianJianTec1711125017991.png)

正如上面说到，前一个加项就是观察方向的相反方向上从实体表面反射的光线经过介质到摄像机位置的radiance，后一个加项就是观察方向的相反方向上从来自精确光源的散射光线经过介质到摄像机位置的radiance。其中$T_r(c,p)$是给定点$x$与相机位置$c$之间的**透光率**；$L_{scat}(x,v)$是**沿着观察射线，在给定点x处散射的光线**。公式中各计算部分如下图所示。

![20240323003358](https://raw.githubusercontent.com/recaeee/PicGo/main/20240323003358.png)

接下来，简单说说该公式中涉及的相关概念，同时也是代码实现中的重要参数（篇幅有限，同时本人理解有限，内容又比较晦涩难讲，多见谅），更详细的内容可以参考原书。

##### 2.3 透光率$T_r$

**透光率$T_r$代表了光线在一定距离内能够通过介质的比例**，简单来说，光线经过介质时会产生距离上的衰减，其数学定义如下。

![20240323004609](https://raw.githubusercontent.com/recaeee/PicGo/main/20240323004609.png)

这种关系也被称为**Beer-Lambert定律**，方程中的光学深度$\tau$是没有单位的，它代表了光线的衰减量。**消光**（extinction，上面提到过哦）**系数**或者传播**距离**越大，光学深度$\tau$也就越大，也就说明通过介质的光线就越少。由于$\sigma_t=\sigma_a+\sigma_s$（上文提到过），因此**透光率会同时受到吸收和外散射的影响**。

![20240323005023](https://raw.githubusercontent.com/recaeee/PicGo/main/20240323005023.png)

##### 2.4 散射事件$L_{scat}$

对于场景中**给定位置x（RayMarching采样点）和方向v（观察方向的相反方向）**，对于精确光源，其在该x点v方向**内散射事件**进行积分可以这样做：

![20240324083516](https://raw.githubusercontent.com/recaeee/PicGo/main/20240324083516.png)

方程中$n$是光源的数量，$p()$是相位函数，$v()$是可见度函数，$l_{c_i}$是第$i$个光源的方向向量，$p_{light_i}$是第i个光源的位置。

看起来很复杂，其实也很简单，只考虑单个光源的情况下，可以理解为**空间中一个点的内散射强度和Phase function（指定方向散射概率）、光源对该点是否可见（是否在阴影中、介质体积衰减）、光源到该点radiance的距离衰减有关**。

**可见性函数$v(x.p_{light_i})$代表了从光源位置$p_{light_i}$处发出的光线最终到达位置$x$的比例**，其数学形式如下：

![20240324084008](https://raw.githubusercontent.com/recaeee/PicGo/main/20240324084008.png)

shadowMap很好理解，即该点是否处于阴影中。**volShad**（体积阴影项）代表从光源位置$p_{light_i}$到采样点$x$的**透光率$T_r$**（第2.3节提到的，吸收和外散射事件），$volShad(x,p_{light_i})=T_r(x,p_{light_i})$。

##### 2.5 相位函数Phase function

终于到了最后一个理论部分，加油！！

在1.4节中提到过，参与介质中单个粒子大小也会影响散射，当光线经过参与介质中的单个粒子，**它在不同方向上的散射概率和粒子半径有关**，即**粒子大小会影响光线在给定方向上发生散射的概率**。在评估内散射事件的时候，可以使用一个相位函数，在宏观层面上描述散射方向的概率方向，也就是**对于一个粒子，给定入射方向，定义出射方向在所有方向上的概率分布**，该函数在单位球体上的积分必须为1。图示如下，很好理解，其中参数$\theta$表示光线的向前传播路径（蓝色）与外散射方向（绿色）之间的夹角。

![20240324085031](https://raw.githubusercontent.com/recaeee/PicGo/main/20240324085031.png)

由于物理上的原因，对于**不同数量级**的粒子半径，Phase function的区别很大：

1. 对于相对（光的波长）尺寸很小的粒子，发生**瑞利散射**，比如空气。

2. 对于相对尺寸接近1的粒子，发生**米氏散射**，常见的粒子为雾中的聚光灯、太阳方向上的云，也就是**体积光对应的Phase function**。
3. 当粒子尺寸明显大于光线波长，发生**几何散射**。

对于体积光的渲染，我们需要用到米氏散射的Phase function，依靠物理学家的研究，常用于**描述米氏散射的一种相位函数是Henyey-Greenstein(HG)相位函数**，可以用来表示任何烟、雾或者灰尘状的参与介质。HG函数数学形式如下：

![20240324085938](https://raw.githubusercontent.com/recaeee/PicGo/main/20240324085938.png)

其中$\theta$即入射光与出射光的夹角。参数$g$是一个[-1，1]之间的可变参数，用来控制前向、后向散射的比例。

该函数的图示如下。

![20240324090127](https://raw.githubusercontent.com/recaeee/PicGo/main/20240324090127.png)

另外，还有一个与HG函数结果相似、但速度更快的近似相位函数Schlick相位函数：

![20240324090404](https://raw.githubusercontent.com/recaeee/PicGo/main/20240324090404.png)

OK，理论部分终于讲完了，其实说复杂也不复杂，说不复杂也复杂，这块算是比较难讲的部分了，需要考虑简洁与详细之间的平衡，讲的不好请见谅，如果有更多的时间，推荐读者可以去阅读RTR4中的第14章。接下来进入实战环节。

#### 3 RecaNoMaho体积光实践

在阅读完上文或阅读完《Real-Time Rendering, Fourth Edition》第十四章“体积与半透明渲染”的光线散射理论部分内容后，我们便大致了解了丁达尔效应的成因、体积光的定性和定量模型，即**关于体积光的理论部分**。接下来，我们就来实践一下体积光在Unity URP中的实现，为了降低实践失败的概率、更快取得实现渲染效果的愉悦感，我们先抛开自己脑海中蠢蠢欲动的想法，参考一下目前网上一些大佬的实践成功案例。虽然在已知理论模型的情况下，从零自己实现一套体积光是大概率可行的，但是一定会踩很多的坑，虽然说这样做一遍一定会让自己对体积光的认识更加地深刻，但代价是耗费更多的时间与精力、碰壁后可能放弃的概率。所以个人认为与其花10天从零开始自己实践，不如做个笨蛋，先参考大佬分享的实践，走在智者走过的道路上。更何况，在参考完他人的实践后，我们也可以继续开创自己的想法。

在RecaNoMaho中，使用了**URP**作为渲染管线，以RenderFeature的形式实现体积光渲染，保证了它可以被方便地被移植与使用。本节的实践中大部分代码参考的文章为作者[SardineFish](https://www.zhihu.com/people/fish-sardine)的[《在 Unity 中实现体积光渲染》](https://zhuanlan.zhihu.com/p/124297905)，感谢大佬的无私分享。该作者主要参考了《Real-Time Rendering, Fourth Edition》的第十四章以及GDC 2016上《Inside》开发者的演讲[《Low Complexity, High Fidelity - INSIDE Rendering》](https://www.gdcvault.com/play/1023002/Low-Complexity-High-Fidelity-INSIDE)进行了一套体积光的实现，同时他将代码实现在了自己基于SRP实现的管线中（因此其实我目前的代码大部分工作也算是将其内容移植到URP管线中，再次感谢大佬的分享，未来我也会参考更多资料对RecaNoMaho体积光进行进一步完善和改良），原作者给出的效果图如下。

![20240303224642](https://raw.githubusercontent.com/recaeee/PicGo/main/20240303224642.png)

在RecaNoMaho体积光初步实践后，实现效果图如下（4次采样，无后处理等其他优化，个人比较喜欢这种颗粒感，因此我去除了一部分噪点，但依然保留了一部分，这样你才知道你渲染的是体积光bushi）。

![58248757c4a0de3426e7d531213fa398](https://raw.githubusercontent.com/recaeee/PicGo/main/58248757c4a0de3426e7d531213fa398.png)

采样次数增加的渲染效果，但随之而来的代价是性能。

![b2979ba4785b490c5c9ad00e5f3c82b9](https://raw.githubusercontent.com/recaeee/PicGo/main/b2979ba4785b490c5c9ad00e5f3c82b9.png)

接下来，我会讲一些实战中的要点，详细的内容可以直接参考RecaNoMaho源码。

##### 3.1 基于Ray-marching的体积光渲染

参考[《游戏开发相关实时渲染技术之体积光》](https://zhuanlan.zhihu.com/p/21425792)，对于体积光的实时渲染，目前工业界已经拥有了**多种多样的实现方式**，比如：

实现非常简单、性能非常好、但是效果非常受限的**基于Billboard片“假”体积光**。

![20240325031601](https://raw.githubusercontent.com/recaeee/PicGo/main/20240325031601.png)

同样性能很好、但使用范围较窄的**基于后处理的径向模糊体积光**。

![20240325031729](https://raw.githubusercontent.com/recaeee/PicGo/main/20240325031729.png)

另外还有**基于体渲染的体积光**，比如[《体渲染探秘（二）基于VoxelVolume实现体积雾》](https://zhuanlan.zhihu.com/p/366083234)。

还有就是，我们本节的主角，**基于Ray-marching的体积光**，也是相对效果很好、适用范围广泛的体积光实现方式。

在实时渲染中，通常会采用**光线步进Ray-marching**的方式实现体积光的渲染，通过**采样光路上的多个步进点从而实现对散射光线和正确透光率的积分**（即2.2、2.3、2.4节中的计算式）。基于Ray-marching的体积光渲染示意图如下。

![20240325032216](https://raw.githubusercontent.com/recaeee/PicGo/main/20240325032216.png)

参考[《在 Unity 中实现体积光渲染》](https://zhuanlan.zhihu.com/p/124297905)，其中提到，实时Ray-marching的代价巨大，因此可以**对模型简化**：

1. 省略从光源入射到介质的透光率Transmittance积分。

2. 估计入射光经过的距离和介质的平均衰减率（考虑均匀介质）。

3. 根据效果需要，决定是否考虑阴影。

可以将体积光中**对光路上散射光线积分的过程**基本表示为以下代码：

```c++
    //观察方向ray，从near到far对散射光线通过Ray-Marching采样积分
    float3 scattering(float3 ray, float near, float far, out float3 transmittance)
    {
        transmittance = 1;
        float3 totalLight = 0;
        float stepSize = (far - near) / _Steps;
        // [UNITY_LOOP]
        for(int i= 1; i <= _Steps; i++)
        {
            float3 pos = _WorldSpaceCameraPos + ray * (near + stepSize * i);
            //从视点到介质中x处的透射率，采用累乘避免多次积分
            transmittance *= exp(-stepSize * extinctionAt(pos));
            
            float3 lightDir;
            //散射光线=从介质中x处到视点的透射（光）率*从光源到介质中x处的散射光线*步进权重*从介质中x处到视点的Phase function（粒子直径对散射方向的影响）
            totalLight += transmittance * lightAt(pos, lightDir) * stepSize * Phase(lightDir, -ray);
        }
        return totalLight;
    }

    //返回介质中x处接收到的光线（RGB），以及x处到光源的方向
    float3 lightAt(float3 pos, out float3 lightDir)
    {
        //_LightPosition.w = 1时，为SpotLight
        lightDir = normalize(_LightPosition.xyz - pos * _LightPosition.w);
        float lightDistance = lerp(_DirLightDistance, distance(_LightPosition.xyz, pos), _LightPosition.w);
        //从介质中x处到视点的消光系数，采用累乘避免多次积分
        float transmittance = lerp(1, exp(-lightDistance * extinctionAt(pos)), _IncomingLoss);

        float3 lightColor = _LightColor.rgb;
        //考虑光源方向与片元到光源方向之间夹角的能量损失
        lightColor *= step(_LightCosHalfAngle, dot(lightDir, _LightDirection.xyz));
        //考虑阴影
        lightColor *= shadowAt(pos);
        //透射率造成的衰减
        lightColor *= transmittance;

        return lightColor;
    }
```

其中**extinctionAt**为2.1节中提到的消光系数，即**吸收和外散射事件对当前光路上散射光线造成的损失**，可以表示为一个恒定值（均匀介质）或者采样3D Texture。**Phase function**（对应2.5节）可以采用不同模型，如HG函数、Schlick函数。**shadowAt**可以采样shadowMap实现。

[原文](https://zhuanlan.zhihu.com/p/124297905)中提到，从视点到介质中x处的透光率$T_r$的计算可以**采用累乘的方式避免多次积分计算**（即指数部分拆分成乘项）。

##### 3.2 确定Ray-marching起点与终点

从代码中可以看出，我们会**从观察射线的深度near开始计算Ray-marching直到深度far**。在实际场景中，比如一个聚光灯照射的场景，聚光灯本身照射的范围就是一个锥体，如果我们从摄像机就开始采样，那么就很容易会出现采样点位于聚光灯照射的锥体外，采样点计算处的光照结果显然为0，因为从光源入射到该点的光线始终为0，导致很多采样点无效，即耗费性能，最终效果也不好。因此，第一个普遍的优化点就是，**确定更精确的Ray-marching起点和终点（即代码中的near和far）**。

拿聚光灯照射的锥体范围举例，我们只需要计算**从观察射线入射锥体范围的起点、与出射锥体范围的终点**，这一段区间内的Rar-marching就足够了。

在[原文](https://zhuanlan.zhihu.com/p/124297905)中，介绍了一种**基于投影矩阵快速得到投影平锥体6个平面的方法**，可以用来**快速计算聚光灯的照射范围平锥体**（注意不是圆锥），文献指路[《Fast Extraction of Viewing Frustum Planes from the WorldView-Projection Matrix》](https://www.gamedevs.org/uploads/fast-extraction-viewing-frustum-planes-from-world-view-projection-matrix.pdf)。具体原理在这里不展开了，在SardineFish佬的文中对该方法的思路进行了简单介绍。

##### 3.3 应用噪声

在基于Ray-marching的体积光渲染中，**应用噪声（抖动采样）是非常非常非常重要的一点**，为什么这么重要呢，我直接拿图来说明吧。

以下是**不应用噪声、采样16次**的结果，可以看到阴影部分成块，并且有很多瑕疵，存在效果不正确的情况。

![20240325033731](https://raw.githubusercontent.com/recaeee/PicGo/main/20240325033731.png)

以下是**应用噪声、采样4次**的结果，可以看到体积光效果非常连续，并且效果正确。

![20240325033858](https://raw.githubusercontent.com/recaeee/PicGo/main/20240325033858.png)

不应用噪声，即使采样16次的效果还不如应用噪声4次后的效果，由此可见对于体积光渲染噪声的重要性。

对于基于Ray-marching的体积光，**噪声应用于对光线步进的起点和终点（即near和far）做一个观察方向上的偏移**（注意是观察方向上），同时该偏移量应该限制在单次Ray-marching步长内。

![20240325034347](https://raw.githubusercontent.com/recaeee/PicGo/main/20240325034347.png)

噪声的来源可以是许多方式，可以是基于随机函数生成的白噪声，也可以是离线生成的噪声图。《Inside》的开发者在GDC上提出了**将Blue Noise用于抖动采样，能够带来更好的渲染效果**。

**Blue Noise是一种高频的随机噪声**，它无法实时生成，在[Christoph Peters的Blog](https://momentsingraphics.de/BlueNoise.html)中对Blue Noise作了深入介绍，并且其中提供了各种尺寸和类型的Blue Noise Textures。不过URP Package中提供了3种尺寸的Blue Noise Textures，因此我就直接拿来用了。

此外，对于体积光渲染，应用噪声的同时搭配**时域抗锯齿TAA**效果会非常好（我用的URP14还没有提供URP原生的TAA，从URP15开始提供了原生的TAA，不过我之前也在实际项目中对URP15的TAA进行了更低版本的移植，同时也实现过多个TAA方案，之后也会加入到RecaNoMaho中，挖坑挖坑~）。

另外参考SardineFish佬的实现，因为体积光的理论模型中存在很多非人性化的参数，直接调整这些参数不利于美术人员直观感受，因此提供了一些更人性化的参数用于调控，这部分就不展开了，可以直接看SardineFish佬的文章。

##### 3.4 更加风格化

在[《[Unity 活动]-游戏专场｜从手机走向主机 -《原神》主机版渲染技术分享》](https://www.bilibili.com/video/BV1Za4y1s7VL/?share_source=copy_web&vd_source=41aba8b1b9773ff3ab25ca40ee3f2bde)中提到，**非常基于物理的体积光效果往往不会很强烈**，比如下图中的效果。

![20240325040249](https://raw.githubusercontent.com/recaeee/PicGo/main/20240325040249.png)

而下面是原神实际游戏中使用的体积光效果。

![20240325040339](https://raw.githubusercontent.com/recaeee/PicGo/main/20240325040339.png)

显然，实际游戏中的体积光更加清晰明亮、明暗对比明显。参考[《[摸着原神学图形]区别于普通体积雾的God Ray》](https://zhuanlan.zhihu.com/p/642361315)，当这么清晰明亮的光路出现时空气中粒子的密度应该是很大的，比如浓雾或很大灰尘，但现实中这种场景体积光效果理应更加朦胧，因此这种效果是不符合现实的（但是是游戏中想要的）。

因此，从体积光理论模型的角度分析，背景清晰意味着**消光系数$\tau_t$（Extinction，即吸收和外散射事件）较小**，同时要有强烈的散射，即反照率$\rho=\frac{\tau_s}{\tau_t}$（这个概念上文中未提到，RTR4第14章节中提到了该概念）非常大。

因此，实现这样风格化的体积光的一种方法是**把吸收$\tau_a$设置得很低，同时拉高透光率$T_r$**。但是这种方法目前在RecaNoMaho中不可行（因为项目中$\tau_a$和$\tau_s$被合并为了1个参数，未来可能会分开）。

另一种方法是对光源到介质的透光率$T_r$拉高，对从介质到相机的透光率$T_r$拉低，这样做非常不物理，但是效果就非常好。

因此，在RecaNoMaho中，目前采用了后者方法对体积光效果进行风格化调整（做的比较粗糙，未来会改良）。

未应用该风格化调整的效果如下。

![20240325043723](https://raw.githubusercontent.com/recaeee/PicGo/main/20240325043723.png)

应用风格化后的效果如下（马马虎虎吧，另外增加了些采样次数，这种方法会导致噪点明显）。

![20240325043823](https://raw.githubusercontent.com/recaeee/PicGo/main/20240325043823.png)

![20240325051007](https://raw.githubusercontent.com/recaeee/PicGo/main/20240325051007.png)

#### 4 结束语

好啦，RecaNoMaho第一篇《从零开始的体积光渲染》目前先实践到这里，实际上到目前为止只做了**从0到0.5的体积光渲染**，只能说是初步实现了体积光的效果，勉强能看，也只适用于Spot Light，到实际运用显然还差了很多，因此这算个上篇？先分p到这里的一个原因是本篇内容感觉已经挺多的了（好累！），另一个原因是我太久没产出啦！（不能堕落在摸鱼的生活中啊）

其实网上目前已经有很多关于体积光的分享与参考资料了，我写的内容大部分也都是已有的一些分享的内容以及我自己的一些体会，后续我也会参考更多资料，进一步对代码进行优化，并且对渲染效果、性能方面进行拓展，也欢迎同学们一起讨论~

最后，分享一些体积光中的Miku~（不要在意Miku的卡通渲染的槽点555，之后我会好好探索下卡通渲染的，埋坑ing）。

![5fd1ffb053ec5e5b90c6eb78d653b9d5](https://raw.githubusercontent.com/recaeee/PicGo/main/5fd1ffb053ec5e5b90c6eb78d653b9d5.png)

![e083f6a973173f2ed3a89a28d09d621e](https://raw.githubusercontent.com/recaeee/PicGo/main/e083f6a973173f2ed3a89a28d09d621e.png)

![b08660e53624335d34a65c7a8b1f9a13](https://raw.githubusercontent.com/recaeee/PicGo/main/b08660e53624335d34a65c7a8b1f9a13.png)

#### 参考
1. https://zhuanlan.zhihu.com/p/124297905
2. https://github.com/Morakito/Real-Time-Rendering-4th-CN/tree/master
3. https://www.gdcvault.com/play/1023002/Low-Complexity-High-Fidelity-INSIDE
4. https://zh.wikipedia.org/wiki/%E5%BB%B7%E5%BE%97%E8%80%B3%E6%95%88%E6%87%89
5. https://zh.wikipedia.org/zh-hans/%E9%AB%94%E7%A9%8D%E5%85%89
6. https://zhuanlan.zhihu.com/p/21425792
7. https://www.gamedevs.org/uploads/fast-extraction-viewing-frustum-planes-from-world-view-projection-matrix.pdf
8. https://momentsingraphics.de/BlueNoise.html
9. https://www.bilibili.com/video/BV1Za4y1s7VL/?share_source=copy_web&vd_source=41aba8b1b9773ff3ab25ca40ee3f2bde
10. https://zhuanlan.zhihu.com/p/642361315
11. https://zhuanlan.zhihu.com/p/630539162