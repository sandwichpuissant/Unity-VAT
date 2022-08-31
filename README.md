# Unity-VAT
Vertex Texture Animation tool for Unity


_Disclaimer: I made this quickly and it might be confusing or/and contains mistakes, use it at your own risk! Also you should be a bit familiar with Unity and ShaderGraph._

<details>
  <summary>Comprehensive explanation</summary>  
  
  Usually when a computer is asked to animate a mesh, it goes through a list of bones, moves and rotates them according to our animation, calculates the influence they have on each vertex and finally sets the positions of these vertices, on a per frame basis. This process is called *skinning* and I wouldn’t wish it upon my worst enemy. The **Vertex Animation Texture** technique allows us to provide the positions of these vertices directly to our beloved computer, avoiding the painful sounding skinning process.  
  
  
  The core idea is to retrieve the vertex positions at different frames of our animation, store this data in a texture and let a shader do what it does best, which is reading this texture to animate our mesh. A 3D vector can represent a position or a direction, and is defined by 3 different values, **X**, **Y** and **Z**, for Xight, Yup and Zorward, [but maybe not](https://twitter.com/freyaholmer/status/1325556229410861056). Luckily for us, computer colors also have 3 values definition, **R**ed, **G**reen and **B**lue (it can also have an additional fourth **A**lpha value but we don’t talk about it). A texture roughly being [just a list of colours](https://en.wikipedia.org/wiki/Pixel), we can work with that and store vectors as colors!  
  
  However, it has a some drawbacks and constraints:  
- **Texture size**  
  Because we’re going to store the positions of EVERY vertex of the mesh for EACH frame of the animation clip, the size of the texture is going to be FrameCount x VertexCount. For a 1000 vertices mesh with a 10 seconds animation sampled at 60 FPS, the texture size would be [x=1000, y=60*100] so a cool 1000 x 600 px texture. You’ll probably want it scaled up to the next power of 2 so a 1024 x 1024 px texture. It is comforting to know that [computers, too, have limits](https://www.youtube.com/watch?v=HdtmmHEs9jg), and the current maximum size a modern graphic cards can handle is 16384 x 16384 px, which would allow us to store a 273 seconds animation sampled at 60FPS (16384 / 60) for a 16 384 vertices mesh. However if you’re reading this you’re probably trying to optimize your game and you definitely don’t want textures that big.

    
- **Precision**  
  The **X**, **Y** and **Z** values of a 3D vector can be whatever they want. For example [x=420, y=-69.69, z=666.999]. However, the **R**, **G** and **B** values of a colors are usually between **0** and **1**, so we’ll have to do a little math to convert these values to a usable position (spoiler: lerping between a min and max). As mentioned earlier, computers have their limits and [they’re not good](https://en.wikipedia.org/wiki/Floating-point_arithmetic#Accuracy_problems) at handling gigantic or extremely precise numbers, such as 420.69666999420420. They’ll just stop at one of the decimals and give up. Our theoric color values will probably be something like 0.284758922748294683... and our computer will get discouraged very fast, rounding this value at the furthest decimal it can deal with. Because we’re essentially converting large values to a [0-1] value, we’ll inevitably lose precision, especially with huge models or very subtle animations. You have been warned.
    
- **Batching**  
  Along with 3D vectors (on its X axis), the texture also stores each vertex ID (on its Y axis), so the shader can later "target" each specific vertex. When static batching happens, meshes are combined to form a new huge single one. This leads to two different issues: the vertex count will be much higher than the one we had when creating the texture, and since we retrieved the vertex positions in object space (meaning these positions are relative to the mesh's origin), the positions will be wrong because the new combined mesh has its own single origin, different from the ones of all its source meshes.  
  
    This tool does not store the positions of our vertices, but instead the offsets from their "default" position (when the mesh is not animated). After retrieving the vertex *offsets* for each sampled frame, it remembers the highest and the lowest ones for each axis to establish the "bounds". In the shader, these bounds are lerped with the color values to estimate the "original" offset of the targeted vertex. Say the bounds are [-20, 10], a color.**r** value of 0.5 means the vertex offset from its default position is 5 on the local **X** axis.  
  
  A texture will usually look like this:  
  
  ![example](https://user-images.githubusercontent.com/15387138/187559754-e6a65309-04d5-4df3-85b7-1a7a41b69797.png)  
  
Unbelievably ugly but powerful, this texture is meant to be "read" from left to right. Each row of pixel is a timeline for a specific vertex, and each colum is a different frame.
  
While the VAT technique is very satisfying to setup, it is best used with fairly small or distant meshes with short looping animations. Its power is fully unleashed when you need a lot of these animated meshes in your scene, or for a model with a lot of bones, e.g. a dense low-poly crowd cheering from a distance or the individual crabs from the [annual crab migration on Christmas Island](https://www.youtube.com/watch?v=IbpvWsBpr4E).
This technique can be used in any game engine, I’ll focus on [Unity](https://www.gamesindustry.biz/unity-pens-deal-that-will-aid-us-defense-homeland-security-and-intelligence) since this feature is not built-in, [unlike, say, Unreal Engine](https://www.dictionary.com/e/emoji/face-with-rolling-eyes-emoji/).  
  
  
</details>

## VAT Tool  
![screenshot](https://user-images.githubusercontent.com/15387138/187550115-0fce295d-cfbd-4785-a89b-f8a9dba36301.png)  

First, you'll need to place the _VATTool.cs_ script inside an **Editor** folder in your project. Once it's there, you'll be able to open its window from the **Tools** menu, and selecting **Vertex Animation Texture Tool**.  

It needs:  
- An **Animation clip**,
- A reference to the **root GameObject of your animation**, which is probably the one where an Animator or Legacy Animation component is attached,
- A reference to a **SkinnedMeshRenderer**

The **sampling rate per second** is the *minimum* rate at which the animation will be sampled. At 60FPS, the tool will sample the animated mesh every 1/60 seconds of the animation clip and retrieve the vertex positions on each of these frames. The width of your texture will be SamplingRate * ClipDuration, BUT if **power of two** is checked, this value will be rounded to the >= power of two, and your texture sample rate will be automatically higher (probably a good thing).  
The **power of two** checkbox will force the texture to have power of two dimensions. Leave it checked to [please the computer gods](https://www.intel.com/content/www/us/en/developer/articles/technical/opengl-performance-tips-power-of-two-textures-have-better-performance.html).  
Once everything is setup, you can click the **Generate** button and hope for the best.

After a successful generation, a new section appears with the last generation results:  

![image](https://user-images.githubusercontent.com/15387138/187556950-6a54c028-004d-400d-90df-438b001e57d7.png)

The **Asset** field is there so you make the Editor focus on the texture in the Project tab when you click on it.  
The **Duration** field is the Animation Clip full duration, in seconds. You'll probably want to multiply something with it inside your shader.  
The **Bounds** field represent the *minimum* and *maximum* offsets registered during generation. You'll need these informations in your shader.
Write them down somewhere or keep the window open.

## Shader
It's up to you to make a shader which will be using a Vertex Animation Texture but I made a ShaderGraph subgraph which acts as a simple VAT texture reader.  

![VertexAnimationTexture002](https://user-images.githubusercontent.com/15387138/187561456-97483cb7-3ff3-4dbf-bc63-15578af6440f.png)  

You'll need to feed it the texture, the bounds you definitely wrote down from earlier, and the normalized time of the animation. If you want to play the animation with the same speed as its clip, you can add a "duration" float property to your shader and do something like this:  

![image](https://user-images.githubusercontent.com/15387138/187562750-1ba9c784-e3bc-4eeb-8d4a-050bdc31daec.png) 
 


